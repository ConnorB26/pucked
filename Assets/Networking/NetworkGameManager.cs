using System.Collections.Generic;
using Cards;
using Effects;
using Gameplay;
using Networking.Snapshots;
using Unity.Netcode;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Network-facing wrapper around CoreGameManager.
    /// Host/server is authoritative. Clients send play requests via RPC.
    ///
    /// This class is the single coordination point between:
    ///   - Server-side game logic (CoreGameManager, in Gameplay/)
    ///   - Client-side state (LocalGameState)
    ///   - Event bus (GameEvents)
    ///
    /// RPC handlers follow a consistent pattern:
    ///   1. Update LocalGameState (so subscribers see current data)
    ///   2. Fire GameEvents (so UI and other systems react)
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

        // Maps: clientId <-> playerId (server-only)
        private readonly Dictionary<ulong, int> _clientIdToPlayerId = new();
        private readonly Dictionary<int, ulong> _playerIdToClientId = new();

        private bool _isGameInitialized;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

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
            if (!IsServer || _core == null) return;

            if (!_clientIdToPlayerId.TryGetValue(clientId, out var playerId))
                return;

            Debug.Log($"[NetworkGameManager] Client {clientId} (player {playerId}) disconnected.");

            _clientIdToPlayerId.Remove(clientId);
            _playerIdToClientId.Remove(playerId);

            if (_core.Phase != GamePhase.InGame) return;

            // HandlePlayerLeft fires CoreGameManager.PlayerEliminated event,
            // which triggers OnCorePlayerEliminated -> PlayerEliminatedRpc.
            // It may also trigger game-over (which sets _core = null via ServerEndGame).
            _core.HandlePlayerLeft(playerId);

            // If game ended due to this disconnect, core events already handled
            // the game-over RPC and cleanup. _core is null now.
            if (_core is not { Phase: GamePhase.InGame }) return;

            SyncAllHands();
            NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
        }

        #endregion

        #region Server: Initialization

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
            _core.GoalieSaveUsed += OnCoreGoalieSaveUsed;
        }

        private void SendInitialSyncToClients()
        {
            foreach (var (clientId, playerId) in _clientIdToPlayerId)
            {
                AssignPlayerIdRpc(playerId, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                SyncHandToClient(clientId, playerId);
            }
        }

        #endregion

        #region Public API (Client -> NetworkGameManager)

        /// <summary>
        /// Called by CardHandController when the local player clicks a card.
        /// targetPlayerId is 0 for untargeted cards; set for cards like Attack.
        /// </summary>
        public void RequestPlayCard(int cardInstanceId, int targetPlayerId = 0)
        {
            if (!IsClient) return;
            RequestPlayCardRpc(cardInstanceId, targetPlayerId);
        }

        /// <summary>
        /// Called by GameHUDController End Turn button. Draws from deck and advances turn.
        /// </summary>
        public void RequestEndTurn()
        {
            if (!IsClient) return;
            RequestEndTurnRpc();
        }

        public void ServerStartGame()
        {
            if (!IsServer) return;
            if (_isGameInitialized) return;

            Debug.Log("[NetworkGameManager] ServerStartGame called.");

            InitializePlayersFromConnectedClients();
            SendInitialSyncToClients();

            NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);

            foreach (var (clientId, playerId) in _clientIdToPlayerId)
            {
                if (MatchPlayerRegistry.TryGetProfile(clientId, out var profile))
                {
                    RegisterPlayerRpc(playerId, profile.displayName,
                        ColorUtility.ToHtmlStringRGB(profile.color));
                }
                else
                {
                    RegisterPlayerRpc(playerId, $"Player {playerId}",
                        ColorUtility.ToHtmlStringRGB(Color.white));
                }
            }

            // Signal all clients the game is ready. Must be LAST so UI shows
            // after all state (hand, turn, roster) is populated.
            GameReadyRpc();

            _isGameInitialized = true;
        }

        public void ServerEndGame()
        {
            if (!IsServer) return;

            if (_core != null)
            {
                Debug.Log("[NetworkGameManager] Ending current game instance.");
                _core.PlayerEliminated -= OnCorePlayerEliminated;
                _core.PeekRequested -= OnCorePeekRequested;
                _core.GameOver -= OnCoreGameOver;
                _core.GoalieSaveUsed -= OnCoreGoalieSaveUsed;
                _core = null;
            }

            _clientIdToPlayerId.Clear();
            _playerIdToClientId.Clear();
            _isGameInitialized = false;
        }

        #endregion

        #region RPCs: Client -> Server

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPlayCardRpc(int cardInstanceId, int targetPlayerId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;

            if (!_clientIdToPlayerId.TryGetValue(senderClientId, out var playerId))
            {
                Debug.LogWarning($"Unknown clientId {senderClientId} in RequestPlayCardRpc.");
                return;
            }

            if (_core.TurnManager.CurrentPlayerId != playerId)
            {
                Debug.LogWarning($"Player {playerId} tried to play out of turn.");
                return;
            }

            var player = _core.Players[playerId];
            var index = player.Hand.FindIndex(ci => ci.InstanceId == cardInstanceId);

            if (index < 0)
            {
                Debug.LogWarning($"Player {playerId} has no card instance {cardInstanceId}");
                return;
            }

            // Capture card info BEFORE PlayCard removes it from hand.
            var cardDef = player.Hand[index].Definition;
            var cardName = cardDef != null ? cardDef.cardName : "Unknown";
            var cardCategory = (int)(cardDef != null ? cardDef.category : 0);

            // Capture whose turn it is before resolving effects.
            // Some effects (Skip, Attack) advance the turn immediately inside PlayCard.
            var turnBefore = _core.TurnManager.CurrentPlayerId;

            // Server-authoritative play. May advance turn (Skip/Attack), but does NOT
            // require an End Turn — the player can keep playing cards first.
            _core.PlayCard(playerId, cardInstanceId, targetPlayerId);

            // Notify everyone what was played (even if game ended — clients should see it).
            CardPlayedRpc(playerId, cardName, cardCategory);

            // Game may have ended during PlayCard (elimination -> game over -> ServerEndGame).
            if (_core == null || _core.Phase != GamePhase.InGame) return;

            SyncAllHands();

            // Only send a turn-change notification if an effect immediately changed the turn
            // (e.g. Skip, Attack). For normal cards, the turn stays and the player clicks End Turn.
            if (_core.TurnManager.CurrentPlayerId != turnBefore)
                NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
        }

        /// <summary>
        /// Client -> Server: player clicked End Turn.
        /// Draws from the deck, handles immediate draw triggers (Puck'd), advances turn.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestEndTurnRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderClientId = rpcParams.Receive.SenderClientId;

            if (!_clientIdToPlayerId.TryGetValue(senderClientId, out var playerId))
            {
                Debug.LogWarning($"Unknown clientId {senderClientId} in RequestEndTurnRpc.");
                return;
            }

            if (_core == null || _core.Phase != GamePhase.InGame) return;

            if (_core.TurnManager.CurrentPlayerId != playerId)
            {
                Debug.LogWarning($"Player {playerId} tried to end turn but it's not their turn.");
                return;
            }

            // Draw + advance turn (may eliminate player if Puck'd is drawn).
            _core.PlayerEndTurn(playerId);

            // Game may have ended (player drew Puck'd -> eliminated -> game over).
            if (_core == null || _core.Phase != GamePhase.InGame) return;

            // Sync hands (player may have drawn a normal card) and broadcast new turn.
            SyncAllHands();
            NotifyTurnChangedRpc(_core.TurnManager.CurrentPlayerId);
        }

        #endregion

        #region RPCs: Server -> Clients

        /// <summary>Tells a specific client what their playerId is.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignPlayerIdRpc(int playerId, RpcParams rpcParams = default)
        {
            LocalGameState.SetLocalPlayerId(playerId);
            Debug.Log($"[Client] Assigned local playerId = {playerId}");
        }

        /// <summary>Syncs a player's hand to the owning client.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void SyncHandRpc(HandSnapshot snapshot, RpcParams rpcParams = default)
        {
            var names = snapshot.names.ToStringArray();

            if (snapshot.playerId == LocalGameState.LocalPlayerId)
            {
                LocalGameState.UpdateHand(snapshot.instanceIds, names, snapshot.categories);
                GameEvents.LocalHandUpdated();
            }

            Debug.Log(
                $"[Client] Hand sync for player {snapshot.playerId}: {snapshot.instanceIds.Length} cards.");
        }

        /// <summary>Broadcast turn change to ALL clients (including host).</summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyTurnChangedRpc(int currentPlayerId, RpcParams rpcParams = default)
        {
            LocalGameState.SetCurrentTurn(currentPlayerId);
            GameEvents.TurnChanged(currentPlayerId);
            Debug.Log($"[Client] Turn changed. Current playerId = {currentPlayerId}");
        }

        /// <summary>Broadcast player elimination to ALL clients (including host).</summary>
        [Rpc(SendTo.Everyone)]
        private void PlayerEliminatedRpc(int playerId, RpcParams rpcParams = default)
        {
            LocalGameState.MarkPlayerEliminated(playerId);
            GameEvents.PlayerEliminated(playerId);
            Debug.Log($"[Client] Player {playerId} eliminated.");
        }

        /// <summary>Send peek results only to the peeking player's client.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void PeekResultRpc(PeekSnapshot snapshot, RpcParams rpcParams = default)
        {
            var names = snapshot.names.ToStringArray();
            GameEvents.PeekResultReceived(names);
            Debug.Log($"[Client] Peek result: {names.Length} cards.");
        }

        /// <summary>Register a player's identity on all clients (name + color).</summary>
        [Rpc(SendTo.Everyone)]
        private void RegisterPlayerRpc(int playerId, string displayName, string colorHtml,
            RpcParams rpcParams = default)
        {
            if (!ColorUtility.TryParseHtmlString("#" + colorHtml, out var color))
                color = Color.white;

            LocalGameState.RegisterPlayer(playerId, displayName, color);
            Debug.Log($"[Client] Registered player {playerId}: {displayName}");
        }

        /// <summary>
        /// Signals all clients that the game has started. Sent LAST in the init
        /// sequence so all state (hand, turn, roster) is already populated.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void GameReadyRpc(RpcParams rpcParams = default)
        {
            LocalGameState.StartGame();
            GameEvents.GameStarted();
            Debug.Log("[Client] Game started.");
        }

        /// <summary>
        /// Notifies all clients what card was played (for notifications, log, animations).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void CardPlayedRpc(int playerId, string cardName, int category,
            RpcParams rpcParams = default)
        {
            GameEvents.CardPlayed(playerId, cardName, (CardCategory)category);
        }

        /// <summary>Broadcast to all clients that a Goalie Save blocked a Puck'd draw.</summary>
        [Rpc(SendTo.Everyone)]
        private void GoalieSaveUsedRpc(int playerId, RpcParams rpcParams = default)
        {
            GameEvents.GoalieSaveUsed(playerId);
        }

        /// <summary>Notify all clients the game is over and who won.</summary>
        [Rpc(SendTo.Everyone)]
        private void GameOverRpc(int winnerPlayerId, RpcParams rpcParams = default)
        {
            Debug.Log($"[Client] Game over. Winner playerId = {winnerPlayerId}");

            // Build winner profile from client-side player info (works on ALL clients).
            var winnerProfile = LocalGameState.Players.TryGetValue(winnerPlayerId, out var info)
                ? new PlayerProfileData(info.DisplayName, info.Color)
                : new PlayerProfileData($"Player {winnerPlayerId}", Color.white);

            // Order matters: GameEnded hides HUD, then GameOver shows game-over panel.
            LocalGameState.EndGame();
            GameEvents.GameEnded();
            GameEvents.GameOver(winnerPlayerId, winnerProfile);
        }

        #endregion

        #region Helpers (Server-side only)

        private void SyncAllHands()
        {
            foreach (var pair in _clientIdToPlayerId)
                SyncHandToClient(pair.Key, pair.Value);
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

        private void OnCoreGoalieSaveUsed(int playerId)
        {
            if (!IsServer) return;
            GoalieSaveUsedRpc(playerId);
        }

        private void OnCoreGameOver(int winnerPlayerId)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkGameManager] Game over. Winner playerId = {winnerPlayerId}");

            GameOverRpc(winnerPlayerId);
            ServerEndGame();

            // Lobby stays in InGame phase intentionally. It resets to ReadyUp
            // only when the host clicks Rematch (via GameUIController -> ServerResetLobby).
        }

        #endregion
    }
}