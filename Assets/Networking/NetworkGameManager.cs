using System.Collections.Generic;
using Cards;
using Effects;
using Gameplay;
using Networking.Snapshots;
using UI;
using Unity.Netcode;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Network-facing wrapper around CoreGameManager.
    /// Host/server is authoritative. Clients send play requests via Rpc to Server.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        #region Inspector

        [Header("Config / References")] [SerializeField]
        private GameConfig gameConfig;

        [SerializeField] private EffectResolver effectResolver;

        #endregion

        #region Fields

        private CoreGameManager _core;

        // Maps: clientId -> playerId, and back
        private readonly Dictionary<ulong, int> _clientIdToPlayerId = new();
        private readonly Dictionary<int, ulong> _playerIdToClientId = new();

        // Client-side: who am I?
        private int _localPlayerId = -1;
        public int LocalPlayerId => _localPlayerId;

        #endregion

        #region Network Lifecycle

        private bool _isGameInitialized;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            Debug.Log("[NetworkGameManager] Server spawned.");

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            base.OnNetworkDespawn();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || _core == null)
                return;

            if (!_clientIdToPlayerId.TryGetValue(clientId, out var playerId))
                return;

            Debug.Log($"[NetworkGameManager] Client {clientId} (player {playerId}) disconnected.");

            // Remove from dictionaries so we don't try to sync them anymore.
            _clientIdToPlayerId.Remove(clientId);
            _playerIdToClientId.Remove(playerId);

            // If we're in-game, treat this as a player leaving:
            if (_core.Phase == GamePhase.InGame)
            {
                _core.HandlePlayerLeft(playerId);

                // Re-sync remaining players' hands.
                SyncAllHands();

                // Notify clients about elimination & potential turn change.
                PlayerEliminatedRpc(playerId);

                if (_core.Phase == GamePhase.InGame)
                {
                    NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
                }
                else
                {
                    Debug.Log("[NetworkGameManager] Game ended due to disconnect.");
                    // TODO: show game over UI on clients and allow host to return to lobby.
                }
            }
        }

        private void InitializePlayersFromConnectedClients()
        {
            _clientIdToPlayerId.Clear();
            _playerIdToClientId.Clear();

            var players = new List<PlayerRuntime>();
            var idx = 0;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                _clientIdToPlayerId[clientId] = idx;
                _playerIdToClientId[idx] = clientId;

                players.Add(new PlayerRuntime(idx));
                idx++;
            }

            _core = new CoreGameManager();
            _core.Init(gameConfig, effectResolver, players);

            _core.PlayerEliminated += OnCorePlayerEliminated;
            _core.PeekRequested += OnCorePeekRequested;
            _core.GameOver += OnCoreGameOver;
        }

        private void InitializeCoreGame()
        {
            // All done in InitializePlayersFromConnectedClients for now.
        }

        private void SendInitialSyncToClients()
        {
            foreach (var pair in _clientIdToPlayerId)
            {
                var clientId = pair.Key;
                var playerId = pair.Value;

                // Assign local playerId on each client
                AssignPlayerIdRpc(playerId, RpcTarget.Single(clientId, RpcTargetUse.Temp));

                // Sync that player's starting hand
                SyncHandToClient(clientId, playerId);
            }
        }

        #endregion

        #region Public API (Client → NetworkGameManager)

        /// <summary>
        /// Called by local UI to request playing a specific card instance.
        /// </summary>
        public void RequestPlayCard(int cardInstanceId)
        {
            if (!IsClient) return;

            RequestPlayCardRpc(cardInstanceId);
        }

        public void ServerStartGame()
        {
            if (!IsServer)
                return;
            if (_isGameInitialized)
                return;

            Debug.Log("[NetworkGameManager] ServerStartGame called by lobby.");

            InitializePlayersFromConnectedClients();
            InitializeCoreGame();
            SendInitialSyncToClients();

            NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
            _isGameInitialized = true;
        }

        public void ServerEndGame()
        {
            if (!IsServer)
                return;

            if (_core != null)
            {
                Debug.Log("[NetworkGameManager] Ending current game instance.");

                // Unsubscribe from core events
                _core.PlayerEliminated -= OnCorePlayerEliminated;
                _core.PeekRequested -= OnCorePeekRequested;
                _core.GameOver -= OnCoreGameOver;

                _core = null;
            }

            _clientIdToPlayerId.Clear();
            _playerIdToClientId.Clear();
            _isGameInitialized = false;
        }

        private void OnCoreGameOver(int winnerPlayerId)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkGameManager] Game over. Winner playerId = {winnerPlayerId}");

            // 1) Tell all clients (including host) that the game ended & who won
            GameOverRpc(winnerPlayerId);

            // 2) Tear down current core game instance
            ServerEndGame();

            // 3) Reset the lobby back to ReadyUp so players can ready for a new game
            var lobby = FindFirstObjectByType<NetworkLobbyManager>();
            if (lobby != null)
            {
                lobby.ServerResetLobby();
            }
            else
            {
                Debug.LogWarning("[NetworkGameManager] No NetworkLobbyManager found to reset lobby.");
            }
        }

        #endregion

        #region RPCs: Client → Server

        /// <summary>
        /// Clients call this to request playing a card by its instance ID.
        /// Uses new Rpc API, sends to server.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPlayCardRpc(int cardInstanceId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;

            if (!_clientIdToPlayerId.TryGetValue(senderClientId, out var playerId))
            {
                Debug.LogWarning($"Unknown clientId {senderClientId} in RequestPlayCardRpc.");
                return;
            }

            // Turn enforcement
            if (_core.TurnManager.CurrentPlayerId != playerId)
            {
                Debug.LogWarning($"Player {playerId} tried to play out of turn.");
                return;
            }

            var player = _core.Players[playerId];

            // Validate the card instance exists in their hand
            var index = player.Hand.FindIndex(ci => ci.InstanceId == cardInstanceId);
            if (index < 0)
            {
                Debug.LogWarning($"Player {playerId} attempted invalid card instance {cardInstanceId}");
                return;
            }

            // Server-authoritative card play (CoreGameManager expects instance ID) :contentReference[oaicite:4]{index=4}
            _core.PlayCard(playerId, cardInstanceId);

            // After state changes, sync all hands to clients
            SyncAllHands();

            // And broadcast whose turn it is now
            NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
        }

        #endregion

        #region RPCs: Server → Clients

        /// <summary>
        /// Assigns a playerId to the client this RPC is sent to.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignPlayerIdRpc(int playerId, RpcParams rpcParams = default)
        {
            _localPlayerId = playerId;
            Debug.Log($"[Client] Assigned local playerId = {playerId}");
        }

        /// <summary>
        /// Syncs a specific player's hand to the client this is sent to.
        /// We send the card instance IDs plus enough definition data for UI.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void SyncHandRpc(HandSnapshot snapshot, RpcParams rpcParams = default)
        {
            var names = snapshot.names.ToStringArray();
            Debug.Log($"[Client] Hand sync for player {snapshot.playerId}: {snapshot.instanceIds.Length} cards.");
        }

        /// <summary>
        /// Broadcast turn changes to all clients (except server).
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void NotifyTurnChangedRpc(int currentPlayerId, RpcParams rpcParams = default)
        {
            // TODO: UI hook:
            // TurnUI.Instance.SetCurrentPlayer(currentPlayerId, LocalPlayerId);
            Debug.Log($"[Client] Turn changed. Current playerId = {currentPlayerId}");
        }

        /// <summary>
        /// Broadcast player elimination to all clients (except server).
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void PlayerEliminatedRpc(int playerId, RpcParams rpcParams = default)
        {
            // TODO: UI hook: show elimination
            Debug.Log($"[Client] Player {playerId} eliminated.");
        }

        /// <summary>
        /// Sends peek results only to the peeking player's client.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void PeekResultRpc(PeekSnapshot snapshot, RpcParams rpcParams = default)
        {
            var names = snapshot.names.ToStringArray();
            Debug.Log($"[Client] Peek result for P{snapshot.playerId}: {names.Length} cards.");
        }

        // Server -> All: notify clients about game over and the winner.
        [Rpc(SendTo.Everyone)]
        private void GameOverRpc(int winnerPlayerId, RpcParams rpcParams = default)
        {
            var isWinner = winnerPlayerId == LocalPlayerId;
            var isHost = NetworkManager.Singleton.IsServer;

            if (!_playerIdToClientId.TryGetValue(winnerPlayerId, out var clientId))
            {
                Debug.LogWarning(
                    $"[Client] Game over but could not find clientId for winner playerId {winnerPlayerId}");
                return;
            }

            if (!MatchPlayerRegistry.TryGetProfile(clientId, out var winnerProfile))
            {
                Debug.LogWarning($"[Client] No profile found for winner clientId {clientId}");
                winnerProfile = new PlayerProfileData($"Player {winnerPlayerId}", Color.white);
            }

            // Show UI
            GameUIController.Instance.ShowGameOver(isWinner, winnerProfile, isHost);

            Debug.Log($"[Client] Game over. Winner playerId = {winnerPlayerId}");
        }

        #endregion

        #region Helpers (Server-side only)

        private void SyncAllHands()
        {
            foreach (var pair in _clientIdToPlayerId)
            {
                SyncHandToClient(pair.Key, pair.Value);
            }
        }

        private void SyncHandToClient(ulong clientId, int playerId)
        {
            var player = _core.Players[playerId];
            var count = player.Hand.Count;

            var instanceIds = new int[count];
            var names = new string[count];
            var categories = new int[count];

            for (var i = 0; i < count; i++)
            {
                var inst = player.Hand[i];
                instanceIds[i] = inst.InstanceId;

                if (inst.Definition != null)
                {
                    names[i] = inst.Definition.cardName;
                    categories[i] = (int)inst.Definition.category;
                }
                else
                {
                    names[i] = string.Empty;
                    categories[i] = 0;
                }
            }

            var snapshot = new HandSnapshot(playerId, instanceIds, names, categories);

            SyncHandRpc(snapshot, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        #endregion

        #region Core Event Handlers (Server-side)

        private void OnCorePlayerEliminated(int playerId)
        {
            if (!IsServer) return;

            PlayerEliminatedRpc(playerId);
        }

        private void OnCorePeekRequested(int playerId, List<CardDefinition> cards)
        {
            if (!IsServer) return;

            if (!_playerIdToClientId.TryGetValue(playerId, out var clientId))
                return;

            var count = cards?.Count ?? 0;
            var names = new string[count];
            var categories = new int[count];

            for (var i = 0; i < count; i++)
            {
                var def = cards[i];
                if (def != null)
                {
                    names[i] = def.cardName;
                    categories[i] = (int)def.category;
                }
                else
                {
                    names[i] = string.Empty;
                    categories[i] = 0;
                }
            }

            var snapshot = new PeekSnapshot(playerId, names, categories);

            PeekResultRpc(snapshot, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        #endregion
    }
}