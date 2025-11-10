using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Core manager class for the Puck'd card game. Handles game flow, turn management,
/// and coordinates between players, cards, and game state.
/// </summary>
[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    #region Singleton Setup

    public static GameManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Game Settings")] [Tooltip("Number of cards each player starts with (not including Save cards)")] [Min(1)]
    public int startingHandSize = 7;

    [Tooltip("Number of Goalie Save cards each player starts with")] [Range(0, 2)]
    public int startingSaveCards = 1;

    [Tooltip("If true, the deck will be shuffled before each new round")]
    public bool shuffleBeforeEachGame = true;

    [Header("Card Pool Setup")] [Tooltip("All card types that can appear in the deck, and how many copies of each")]
    public List<CardEntry> cardEntries = new();

    [Header("Game State")] [ReadOnly] [SerializeField]
    private List<PlayerView> playerViews = new();

    [ReadOnly] [SerializeField] private int deckSize;

    [ReadOnly] [SerializeField] private int discardPileSize;

    [Header("Current Turn Info")] [ReadOnly] [SerializeField]
    private string currentPlayerName;

    [ReadOnly] [SerializeField] private int currentPlayerIndex;

    [ReadOnly] [SerializeField] private int activePlayerCount;

    [ReadOnly] [SerializeField] private int currentExtraTurns;

    [ReadOnly] [SerializeField] private bool isGameOver;

    [Header("Debug Statistics")] [ReadOnly] [SerializeField]
    private int totalTurnsPlayed;

    [ReadOnly] [SerializeField] private int totalCardsPlayed;

    [ReadOnly] [SerializeField] private int puckdCardsDrawn;

    [ReadOnly] [SerializeField] private int successfulSaves;

    #endregion

    #region Private Fields

    private readonly List<Player> _players = new();
    private Deck _deck;

    #endregion

    #region Events

    // Events for UI and other systems to hook into
    public event Action<Player> OnPlayerTurnStart;
    public event Action<Player> OnPlayerTurnEnd;
    public event Action<Player> OnPlayerEliminated;
    public event Action<Player> OnGameOver;

    #endregion

    #region Public Properties

    public IReadOnlyList<Player> Players => _players;
    public Deck Deck => _deck ??= new Deck();
    public Player CurrentPlayer => _players.Count > 0 ? _players[currentPlayerIndex] : null;
    public CardActionHandler CardHandler { get; private set; }
    public bool IsGameInProgress => !isGameOver && _players.Count > 0;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _deck = new Deck();
        CardHandler = new CardActionHandler(this);
    }

    private void Update()
    {
        UpdateInspectorViews();
    }

    private void UpdateInspectorViews()
    {
        playerViews = _players.Select(PlayerView.FromPlayer).ToList();
        deckSize = _deck?.CardsRemaining ?? 0;
        discardPileSize = _deck?.CardsDiscarded ?? 0;
        activePlayerCount = _players.Count(p => !p.IsEliminated);

        // Update current turn info
        if (CurrentPlayer != null)
        {
            currentPlayerName = CurrentPlayer.Name;
        }
    }

    #region Game Flow

    /// <summary>
    /// Start a new game with the given player names
    /// </summary>
    public void StartGame(List<string> playerNames)
    {
        // Reset statistics
        totalTurnsPlayed = 0;
        totalCardsPlayed = 0;
        puckdCardsDrawn = 0;
        successfulSaves = 0;

        if (playerNames == null || playerNames.Count < 2)
        {
            Debug.LogError("GameManager: Need at least 2 players to start!");
            return;
        }

        // Reset game state
        isGameOver = false;
        currentPlayerIndex = 0;
        currentExtraTurns = 0;
        _players.Clear();
        currentPlayerName = "";

        // Initialize deck
        var expandedPool = ExpandCardPool(cardEntries);
        if (expandedPool.Count == 0)
        {
            Debug.LogError("GameManager: Card pool is empty! Add CardEntries in the Inspector.");
            return;
        }

        if (shuffleBeforeEachGame)
            expandedPool = expandedPool.OrderBy(_ => Random.value).ToList();

        _deck.Initialize(expandedPool);

        // Setup players
        foreach (var (playerName, index) in playerNames.Select((n, i) => (n, i)))
        {
            var player = new Player(playerName, index);
            _players.Add(player);

            // Deal starting hand
            for (var j = 0; j < startingHandSize; j++)
                player.DrawCard(_deck);

            // Deal save cards
            for (var s = 0; s < startingSaveCards; s++)
            {
                var saveCardData = expandedPool.FirstOrDefault(c => c.type == CardType.Save);
                if (saveCardData == null) continue;

                var saveCard = new Card(saveCardData, player);
                player.AddCardToHand(saveCard);
            }
        }

        Debug.Log($"Game started with {_players.Count} players and {_deck.CardsRemaining} cards!");
        StartTurn();
    }

    private void StartTurn()
    {
        if (isGameOver) return;

        totalTurnsPlayed++;

        var player = _players[currentPlayerIndex];
        if (player.IsEliminated)
        {
            AdvanceTurn();
            return;
        }

        currentPlayerName = player.Name;
        Debug.Log($"=== {player.Name}'s turn (Turn #{totalTurnsPlayed}) ===");
        OnPlayerTurnStart?.Invoke(player);
    }

    /// <summary>
    /// End the current player's turn
    /// </summary>
    public void EndTurn()
    {
        if (isGameOver) return;

        var player = CurrentPlayer;
        if (player == null) return;

        OnPlayerTurnEnd?.Invoke(player);

        // Draw phase
        DrawCardForPlayer(player);

        // Check if we need to stay on this player for extra turns
        if (currentExtraTurns > 0)
        {
            currentExtraTurns--;
            Debug.Log($"{player.Name} has {currentExtraTurns} extra turns remaining!");
            StartTurn();
            return;
        }

        AdvanceTurn();
    }

    private void DrawCardForPlayer(Player player)
    {
        var drawnCard = _deck.Draw();
        if (drawnCard == null)
        {
            Debug.Log("Deck empty! Shuffling discard pile...");
            _deck.Shuffle();
            drawnCard = _deck.Draw();
        }

        if (drawnCard == null)
        {
            Debug.LogWarning("Deck is still empty after shuffle — ending turn.");
            return;
        }

        // Handle drawing a Puck'd
        if (drawnCard.Data.type == CardType.Puckd)
        {
            puckdCardsDrawn++;
            Debug.Log($"{player.Name} drew a {drawnCard.Data.cardName}! (Total Puck'd draws: {puckdCardsDrawn})");
            var saved = player.TryUseSaveCard();

            if (saved)
            {
                successfulSaves++;
                var randIndex = Random.Range(0, _deck.CardsRemaining);
                _deck.ReinsertPuckd(drawnCard, randIndex);
                Debug.Log($"{player.Name} saved themselves! (Total saves: {successfulSaves})");
            }
            else
            {
                player.Eliminate();
                OnPlayerEliminated?.Invoke(player);
                CheckGameOver();
            }
        }
        else
        {
            player.AddCardToHand(drawnCard);
            Debug.Log($"{player.Name} drew {drawnCard.Data.cardName}");
        }
    }

    private void AdvanceTurn()
    {
        var previousIndex = currentPlayerIndex;
        var activePlayers = _players.Count(p => !p.IsEliminated);

        if (activePlayers <= 1)
        {
            CheckGameOver();
            return;
        }

        // Find next non-eliminated player
        do
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % _players.Count;

            // Prevent infinite loop if somehow all players are eliminated
            if (currentPlayerIndex == previousIndex)
            {
                Debug.LogError("Turn advancement stuck! Checking game over...");
                CheckGameOver();
                return;
            }
        } while (_players[currentPlayerIndex].IsEliminated);

        StartTurn();
    }

    private void CheckGameOver()
    {
        var active = _players.Where(p => !p.IsEliminated).ToList();
        if (active.Count <= 1)
        {
            isGameOver = true;
            var winner = active.FirstOrDefault();
            Debug.Log($"🏆 Game Over! {(winner != null ? winner.Name : "No one")} wins!");
            OnGameOver?.Invoke(winner);
        }
    }

    #endregion

    #region Game Actions

    /// <summary>
    /// Force a player to take extra turns (used by Attack cards)
    /// </summary>
    public void AddExtraTurns(int amount)
    {
        currentExtraTurns += amount;
        Debug.Log($"Added {amount} extra turns. Total extra turns: {currentExtraTurns}");
    }

    #endregion

    #region Helper Methods

    private List<CardData> ExpandCardPool(List<CardEntry> entries)
    {
        return entries
            .Where(entry => entry.cardData != null)
            .SelectMany(entry => Enumerable.Repeat(entry.cardData, entry.count))
            .ToList();
    }

    #endregion
}