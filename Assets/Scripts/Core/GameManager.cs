using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Singleton")] public static GameManager Instance; // global access for UI or other systems

    [Header("Game Settings")] [Tooltip("Number of cards each player starts with (not including Save cards).")] [Min(1)]
    public int startingHandSize = 7;

    [Tooltip("Number of Goalie Save cards each player starts with.")] [Range(0, 2)]
    public int startingSaveCards = 1;

    [Tooltip("If true, the deck will be shuffled before each new round.")]
    public bool shuffleBeforeEachGame = true;

    [Header("Card Pool Setup")] [Tooltip("All card types that can appear in the deck, and how many copies of each.")]
    public List<CardEntry> cardEntries = new();

    [Header("Runtime State (Read Only)")] [SerializeField, ReadOnly]
    private List<Player> players = new();

    [SerializeField, ReadOnly] private Deck deck = new();
    [SerializeField, ReadOnly] private int currentPlayerIndex = 0;
    [SerializeField, ReadOnly] private bool isGameOver = false;

    // Public accessors
    public IReadOnlyList<Player> Players => players;
    public Deck Deck => deck;
    public Player CurrentPlayer => players.Count > 0 ? players[currentPlayerIndex] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Called from lobby setup or test script
    public void StartGame(List<string> playerNames)
    {
        if (playerNames == null || playerNames.Count < 2)
        {
            Debug.LogError("GameManager: Need at least 2 players to start!");
            return;
        }

        // Build full card list from entries
        var expandedPool = ExpandCardPool(cardEntries);
        if (expandedPool.Count == 0)
        {
            Debug.LogError("GameManager: Card pool is empty! Add CardEntries in the Inspector.");
            return;
        }

        if (shuffleBeforeEachGame)
            expandedPool = expandedPool.OrderBy(x => Random.value).ToList();

        deck.Initialize(expandedPool);
        players.Clear();

        // Create and deal to players
        foreach (var (playerName, index) in playerNames.Select((n, i) => (n, i)))
        {
            var player = new Player(playerName, index);
            players.Add(player);

            // Starting hand
            for (var j = 0; j < startingHandSize; j++)
                player.DrawCard(deck);

            // Starting save(s)
            for (var s = 0; s < startingSaveCards; s++)
            {
                var saveCardData = expandedPool.FirstOrDefault(c => c.type == CardType.Save);
                if (saveCardData == null) continue;
                
                var saveCard = new Card(saveCardData, player);
                player.Hand.Add(saveCard);
            }
        }

        isGameOver = false;
        currentPlayerIndex = 0;
        Debug.Log($"Game started with {players.Count} players and {deck.CountRemaining()} cards!");
        StartTurn();
    }

    private List<CardData> ExpandCardPool(List<CardEntry> entries)
    {
        var list = new List<CardData>();
        foreach (var entry in entries)
        {
            if (entry.cardData == null) continue;
            for (var i = 0; i < entry.count; i++)
                list.Add(entry.cardData);
        }

        return list;
    }

    private void StartTurn()
    {
        if (isGameOver) return;

        var player = players[currentPlayerIndex];
        if (player.IsEliminated)
        {
            AdvanceTurn();
            return;
        }

        Debug.Log($"=== {player.Name}'s turn ===");
        Debug.Log($"Hand: {string.Join(", ", player.Hand.Select(c => c.Data.cardName))}");
    }

    public void EndTurn()
    {
        if (isGameOver) return;
        var player = players[currentPlayerIndex];

        var drawnCard = deck.Draw();
        if (drawnCard == null)
        {
            Debug.Log("Deck empty! Shuffling discard pile...");
            deck.Shuffle();
            drawnCard = deck.Draw();
        }

        if (drawnCard == null)
        {
            Debug.LogWarning("Deck is still empty after shuffle — ending turn.");
            AdvanceTurn();
            return;
        }

        // Handle drawing a Puck’d
        if (drawnCard.Data.type == CardType.Puckd)
        {
            Debug.Log($"{player.Name} drew a {drawnCard.Data.cardName}!");
            var saved = player.TryUseSaveCard();

            if (saved)
            {
                var randIndex = Random.Range(0, deck.CountRemaining());
                deck.ReinsertPuckd(drawnCard, randIndex);
                Debug.Log($"{player.Name} saved themselves! Reinserted Puck’d card.");
            }
            else
            {
                player.Eliminate();
                CheckGameOver();
            }
        }
        else
        {
            player.Hand.Add(drawnCard);
            player.PlayerUI?.RefreshHand(player.Hand);
            Debug.Log($"{player.Name} drew {drawnCard.Data.cardName}");
        }

        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        StartTurn();
    }

    private void CheckGameOver()
    {
        var active = players.Where(p => !p.IsEliminated).ToList();
        if (active.Count <= 1)
        {
            isGameOver = true;
            var winner = active.FirstOrDefault();
            Debug.Log($"🏆 Game Over! {(winner != null ? winner.Name : "No one")} wins!");
        }
    }
}