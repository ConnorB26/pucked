using System;
using Cards;
using Networking;
using Networking.Snapshots;

/// <summary>
/// Static event bus for game-wide notifications. Subscribe in OnEnable, unsubscribe in OnDisable.
/// Fire methods are called by NetworkGameManager RPC handlers after updating LocalGameState.
/// Intentionally has no namespace so every layer can use it without extra usings.
/// </summary>
public static class GameEvents
{
    // ---- Lobby ----

    /// <summary>Full lobby state snapshot received (join, leave, ready, phase change).</summary>
    public static event Action<LobbyStateSnapshot> OnLobbyUpdated;

    // ---- Game Lifecycle ----

    /// <summary>All initial data is synced and the game HUD should show.</summary>
    public static event Action OnGameStarted;

    /// <summary>Game is no longer active. HUD should hide. Fires BEFORE OnGameOver.</summary>
    public static event Action OnGameEnded;

    /// <summary>Winner determined. Game-over UI should show. Fires AFTER OnGameEnded.</summary>
    public static event Action<int, PlayerProfileData> OnGameOver; // winnerId, winnerProfile

    // ---- Hand ----

    /// <summary>Local player's hand has changed (card played, card drawn, initial deal).</summary>
    public static event Action OnLocalHandUpdated;

    // ---- Turn ----

    /// <summary>Current turn player changed. Param: new current playerId.</summary>
    public static event Action<int> OnTurnChanged;

    // ---- Player Events ----

    /// <summary>A player was eliminated (from card effect or disconnect).</summary>
    public static event Action<int> OnPlayerEliminated; // playerId

    // ---- Card Events ----

    /// <summary>A card was played by any player. For notifications, log, animations.</summary>
    public static event Action<int, string, CardCategory> OnCardPlayed; // playerId, cardName, category

    /// <summary>Peek results received by the local player.</summary>
    public static event Action<string[]> OnPeekResult; // card names

    /// <summary>A Goalie Save was automatically used to block a Puck'd draw.</summary>
    public static event Action<int> OnGoalieSaveUsed; // playerId who was saved

    // ---- Fire Methods ----

    public static void LobbyUpdated(LobbyStateSnapshot snapshot) =>
        OnLobbyUpdated?.Invoke(snapshot);

    public static void GameStarted() =>
        OnGameStarted?.Invoke();

    public static void GameEnded() =>
        OnGameEnded?.Invoke();

    public static void GameOver(int winnerId, PlayerProfileData winnerProfile) =>
        OnGameOver?.Invoke(winnerId, winnerProfile);

    public static void LocalHandUpdated() =>
        OnLocalHandUpdated?.Invoke();

    public static void TurnChanged(int currentPlayerId) =>
        OnTurnChanged?.Invoke(currentPlayerId);

    public static void PlayerEliminated(int playerId) =>
        OnPlayerEliminated?.Invoke(playerId);

    public static void CardPlayed(int playerId, string cardName, CardCategory category) =>
        OnCardPlayed?.Invoke(playerId, cardName, category);

    public static void PeekResultReceived(string[] cardNames) =>
        OnPeekResult?.Invoke(cardNames);

    public static void GoalieSaveUsed(int playerId) =>
        OnGoalieSaveUsed?.Invoke(playerId);
}