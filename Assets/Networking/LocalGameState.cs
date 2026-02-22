using System.Collections.Generic;
using Cards;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Client-side game state populated by network RPCs.
    /// Pure data store — no events. Subscribe to GameEvents for notifications.
    ///
    /// Every client (including host) has this. The host also has CoreGameManager
    /// with the full authoritative state, but the host's UI reads from here
    /// identically to any other client.
    ///
    /// The Gameplay/ folder (CoreGameManager, TurnManager, DeckManager, etc.)
    /// is server/host-only. This class is the client-side equivalent — it holds
    /// only what the client is allowed to know.
    /// </summary>
    public static class LocalGameState
    {
        public static int LocalPlayerId { get; private set; } = -1;
        public static int CurrentTurnPlayerId { get; private set; } = -1;
        public static bool IsGameActive { get; private set; }

        public static bool IsMyTurn =>
            IsGameActive && LocalPlayerId >= 0 && LocalPlayerId == CurrentTurnPlayerId;

        /// <summary>Cards in the local player's hand (rebuilt on every sync).</summary>
        public static readonly List<ClientCardData> Hand = new();

        /// <summary>All players in the match, keyed by playerId.</summary>
        public static readonly Dictionary<int, ClientPlayerInfo> Players = new();

        // ---- Setters (called by NetworkGameManager RPC handlers) ----

        public static void SetLocalPlayerId(int id) => LocalPlayerId = id;

        public static void RegisterPlayer(int playerId, string displayName, Color color)
        {
            Players[playerId] = new ClientPlayerInfo
            {
                PlayerId = playerId,
                DisplayName = displayName,
                Color = color,
                IsEliminated = false
            };
        }

        public static void UpdateHand(int[] instanceIds, string[] names, int[] categories)
        {
            Hand.Clear();

            for (var i = 0; i < instanceIds.Length; i++)
            {
                Hand.Add(new ClientCardData
                {
                    InstanceId = instanceIds[i],
                    Name = i < names.Length ? names[i] : string.Empty,
                    Category = i < categories.Length ? (CardCategory)categories[i] : 0
                });
            }
        }

        public static void SetCurrentTurn(int playerId) => CurrentTurnPlayerId = playerId;

        public static void MarkPlayerEliminated(int playerId)
        {
            if (!Players.TryGetValue(playerId, out var info)) return;

            info.IsEliminated = true;
            Players[playerId] = info;
        }

        public static void StartGame() => IsGameActive = true;

        public static void EndGame()
        {
            IsGameActive = false;
            Hand.Clear();
        }

        /// <summary>
        /// Full reset between matches. Call when returning to lobby.
        /// </summary>
        public static void Reset()
        {
            LocalPlayerId = -1;
            CurrentTurnPlayerId = -1;
            IsGameActive = false;
            Hand.Clear();
            Players.Clear();
        }

        // ---- Helpers ----

        /// <summary>
        /// Looks up a player's display name, with fallback for unknown IDs.
        /// </summary>
        public static string GetPlayerName(int playerId)
        {
            return Players.TryGetValue(playerId, out var info)
                ? info.DisplayName
                : $"Player {playerId}";
        }
    }

    /// <summary>
    /// Lightweight card data for client-side display.
    /// No reference to CardDefinition (that only exists on the server).
    /// </summary>
    public struct ClientCardData
    {
        public int InstanceId;
        public string Name;
        public CardCategory Category;
    }

    /// <summary>
    /// Player info known to all clients (broadcast at game start).
    /// </summary>
    public struct ClientPlayerInfo
    {
        public int PlayerId;
        public string DisplayName;
        public Color Color;
        public bool IsEliminated;
    }
}
