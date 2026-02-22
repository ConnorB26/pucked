using System;
using System.Collections.Generic;
using System.Linq;
using Networking.Snapshots;
using Unity.Netcode;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Handles pre-game lobby behavior:
    /// - Tracks connected players (clientId, profile, ready flag).
    /// - Only allows joining while in ReadyUp phase.
    /// - All players must be ready before host can start the game.
    /// - Signals NetworkGameManager (same scene) to start/stop matches.
    /// - Sends minimal events to clients for join/leave/ready.
    /// </summary>
    public class NetworkLobbyManager : NetworkBehaviour
    {
        #region Types

        public enum LobbyPhase
        {
            WaitingForPlayers,
            ReadyUp,
            InGame
        }

        [Serializable]
        private class LobbyPlayer
        {
            // clientId, profile and isReady are intentionally mutable to reflect lobby updates.
            public ulong clientId;
            public PlayerProfileData profile;
            public bool isReady;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Authoritative list of players on the server, keyed by clientId.
        /// </summary>
        private readonly Dictionary<ulong, LobbyPlayer> _players = new();

        /// <summary>
        /// Current lobby phase (WaitingForPlayers, ReadyUp, InGame).
        /// </summary>
        private LobbyPhase _phase = LobbyPhase.ReadyUp;

        /// <summary>
        /// Fallback maximum players used when relay bootstrap value is not set.
        /// </summary>
        private const int FallbackMaxPlayers = 4;

        #endregion

        // UI events are now fired via the central GameEvents bus.
        // Subscribe to GameEvents.OnLobbyUpdated instead of a local event.

        #region Unity / Network lifecycle

        /// <summary>
        /// Called when the NetworkBehaviour is spawned. Sets up server callbacks and submits local profile on clients.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log("[Lobby] Server lobby spawned.");

                _phase = LobbyPhase.ReadyUp;

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                const ulong hostId = NetworkManager.ServerClientId;
                if (!_players.ContainsKey(hostId))
                {
                    AddPlayer(hostId, new PlayerProfileData($"Host {hostId}", Color.white));
                }
            }

            // Client-side auto-profile submit (runs on host AND joining clients)
            if (!IsClient) return;

            var localProfile = LocalPlayerProfile.LoadOrDefault();
            SubmitLocalProfile(localProfile);
        }

        /// <summary>
        /// Called when the NetworkBehaviour is despawned. Removes server callbacks.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Server: connection handling

        /// <summary>
        /// Server callback when a client connects. Adds a placeholder player if needed or rejects if game in progress.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            // If joining during an active game, reject immediately.
            if (_phase != LobbyPhase.ReadyUp && _phase != LobbyPhase.WaitingForPlayers)
            {
                Debug.Log($"[Lobby] Rejecting client {clientId} - game already in progress.");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            // Add with a placeholder profile; client will immediately send its real profile
            if (!_players.ContainsKey(clientId))
            {
                AddPlayer(clientId, new PlayerProfileData($"Player {clientId}", Color.gray));
            }
        }

        /// <summary>
        /// Server callback when a client disconnects. Removes player and broadcasts updated lobby.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (_players.Remove(clientId))
            {
                Debug.Log($"[Lobby] Client {clientId} removed from lobby.");

                // When someone leaves, we send a snapshot with them removed.
                BroadcastLobbyState();
            }
        }

        /// <summary>
        /// Adds a LobbyPlayer entry for the provided client id.
        /// </summary>
        private void AddPlayer(ulong clientId, PlayerProfileData profile)
        {
            var lp = new LobbyPlayer
            {
                clientId = clientId,
                profile = profile,
                isReady = false
            };

            _players[clientId] = lp;
        }

        #endregion

        #region Public API (called by local UI)

        /// <summary>
        /// Called by the local lobby UI on clients to submit profile info.
        /// This should be done once when entering the game scene,
        /// using the profile set on the main menu.
        /// </summary>
        public void SubmitLocalProfile(PlayerProfileData profile)
        {
            if (!IsClient)
                return;

            // Profiles are "fixed" here: we don't support editing them inside the lobby/game.
            SubmitProfileRpc(profile.displayName, ColorUtility.ToHtmlStringRGB(profile.color));
        }

        /// <summary>
        /// Called by lobby UI when player clicks Ready/Unready.
        /// This only affects isReady, not profile data.
        /// </summary>
        public void SetLocalReady(bool ready)
        {
            if (!IsClient)
                return;

            ToggleReadyRpc(ready);
        }

        /// <summary>
        /// Called by the host's UI when pressing "Start Game".
        /// </summary>
        public void HostRequestStartGame()
        {
            if (!IsClient)
                return;

            RequestStartGameRpc();
        }

        /// <summary>
        /// Called by a host-only UI when they want to end the match and
        /// go back to a fresh ReadyUp lobby.
        /// </summary>
        public void HostRequestResetLobby()
        {
            if (!IsClient)
                return;

            if (!IsHost) return;

            // Host is also the server, so just call server-side method directly.
            ServerResetLobby();
        }

        #endregion

        #region RPCs: Client -> Server

        /// <summary>
        /// Client -> Server RPC to submit a player's profile (display name + color).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitProfileRpc(string displayName, string colorHtml, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderId = rpcParams.Receive.SenderClientId;

            if (!ColorUtility.TryParseHtmlString("#" + colorHtml, out var color))
            {
                color = Color.white;
            }

            // Ensure there is a LobbyPlayer entry for this client.
            if (!_players.TryGetValue(senderId, out var lp))
            {
                Debug.LogWarning($"[Lobby] SubmitProfile from unknown client {senderId}, auto-creating LobbyPlayer.");

                // Treat this as first-time join for safety
                var profile = new PlayerProfileData(displayName, color);
                AddPlayer(senderId, profile);
            }
            else
            {
                // Update existing entry's profile
                lp.profile = new PlayerProfileData(displayName, color);
                _players[senderId] = lp;
            }

            // Profiles don't change often (only once per join), so snapshot is fine here.
            BroadcastLobbyState();
        }

        /// <summary>
        /// Client -> Server RPC to toggle the sender's ready flag.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderId = rpcParams.Receive.SenderClientId;

            if (!_players.TryGetValue(senderId, out var lp))
                return;

            lp.isReady = ready;
            _players[senderId] = lp;

            BroadcastLobbyState();
        }

        /// <summary>
        /// Client -> Server RPC for requesting the host-only start-game action.
        /// Only the host/owner is permitted to invoke this successfully.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestStartGameRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            var senderId = rpcParams.Receive.SenderClientId;
            if (senderId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[Lobby] Non-host client {senderId} tried to start game.");
                return;
            }

            TryStartGameOnServer();
        }

        #endregion

        #region Server: lobby logic

        /// <summary>
        /// Validates ready state and transitions to InGame, then instructs NetworkGameManager to start.
        /// </summary>
        private void TryStartGameOnServer()
        {
            if (_phase != LobbyPhase.ReadyUp)
            {
                Debug.LogWarning("[Lobby] Cannot start game when not in ReadyUp phase.");
                return;
            }

            if (_players.Values.Any(p => !p.isReady))
            {
                Debug.LogWarning("[Lobby] Not all players are ready.");
                return;
            }

            Debug.Log("[Lobby] All players ready. Starting game.");

            _phase = LobbyPhase.InGame;

            // Push current player profiles into the static match registry
            // so NetworkGameManager can build PlayerRuntime names from them.
            var profileMap = new Dictionary<ulong, PlayerProfileData>();
            foreach (var kvp in _players)
            {
                profileMap[kvp.Key] = kvp.Value.profile;
            }

            MatchPlayerRegistry.SetAll(profileMap);

            // Tell the NetworkGameManager (in the same scene) to start the match
            var gameMgr = FindFirstObjectByType<NetworkGameManager>();
            if (gameMgr == null)
            {
                Debug.LogError("[Lobby] No NetworkGameManager found in scene.");
                return;
            }

            gameMgr.ServerStartGame();

            // Phase change is relatively rare; snapshot is fine.
            BroadcastLobbyState();
        }

        /// <summary>
        /// Server-side reset back to a fresh ReadyUp state.
        /// Host will typically call this after a game ends.
        /// </summary>
        public void ServerResetLobby()
        {
            if (!IsServer)
                return;

            Debug.Log("[Lobby] Resetting to ReadyUp phase.");

            _phase = LobbyPhase.ReadyUp;

            foreach (var lp in _players.Values)
            {
                lp.isReady = false;
            }

            MatchPlayerRegistry.Clear();

            BroadcastLobbyState();
        }

        #endregion

        #region RPCs: Server -> Clients (lobby sync + events)

        /// <summary>
        /// Sends a full lobby snapshot to all clients.
        /// Used for join/leave, profile submission, phase changes, and full reset.
        /// </summary>
        private void BroadcastLobbyState()
        {
            if (!IsServer) return;

            var count = _players.Count;
            var clientIds = new ulong[count];
            var names = new string[count];
            var colors = new string[count];
            var readyFlags = new bool[count];

            var i = 0;
            foreach (var kvp in _players)
            {
                clientIds[i] = kvp.Key;
                names[i] = kvp.Value.profile.displayName;
                colors[i] = ColorUtility.ToHtmlStringRGB(kvp.Value.profile.color);
                readyFlags[i] = kvp.Value.isReady;
                i++;
            }

            var maxPlayers = RelayBootstrap.MaxConnections > 0
                ? RelayBootstrap.MaxConnections
                : FallbackMaxPlayers;

            var snapshot = new LobbyStateSnapshot(
                (int)_phase,
                clientIds,
                names,
                colors,
                readyFlags,
                maxPlayers
            );

            ApplyLobbySnapshotLocal(snapshot);
            SyncLobbyStateRpc(snapshot);
        }

        /// <summary>
        /// Client RPC: full snapshot received by clients. Updates their local view.
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void SyncLobbyStateRpc(LobbyStateSnapshot snapshot, RpcParams rpcParams = default)
        {
            ApplyLobbySnapshotLocal(snapshot);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Applies a full lobby snapshot locally (used by both server and clients).
        /// Updates internal phase and fires the UI event.
        /// </summary>
        private void ApplyLobbySnapshotLocal(LobbyStateSnapshot snapshot)
        {
            _phase = (LobbyPhase)snapshot.phase;

            var clientIds = snapshot.clientIds;

            Debug.Log($"[Lobby] Snapshot applied locally. Phase={_phase}, players={clientIds.Length}");

            // Fire via central event bus so any system can react.
            GameEvents.LobbyUpdated(snapshot);
        }

        /// <summary>
        /// True when the local client is the host (server).
        /// </summary>
        public bool IsLocalClientHost =>
            IsServer && NetworkManager.Singleton.LocalClientId == NetworkManager.ServerClientId;

        /// <summary>
        /// Server helper to force broadcast of the lobby snapshot.
        /// </summary>
        public void ServerForceLobbySnapshot()
        {
            if (!IsServer) return;
            BroadcastLobbyState();
        }

        #endregion
    }
}