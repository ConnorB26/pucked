using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the game's deck of cards, including draw and discard piles
/// </summary>
public class Deck
{
    private Stack<Card> _drawPile = new();
    private readonly List<Card> _discardPile = new();

    /// <summary>
    /// Get the number of cards remaining in the draw pile
    /// </summary>
    public int CardsRemaining => _drawPile.Count;

    /// <summary>
    /// Get the number of cards in the discard pile
    /// </summary>
    public int CardsDiscarded => _discardPile.Count;

    /// <summary>
    /// Initializes the deck with a list of card data
    /// </summary>
    /// <param name="allCardData">List of CardData to create cards from</param>
    public void Initialize(List<CardData> allCardData)
    {
        if (allCardData == null || allCardData.Count == 0)
        {
            Debug.LogError("Cannot initialize deck with empty card list!");
            return;
        }

        // Shuffle incoming CardData and create Card instances
        var shuffled = allCardData.OrderBy(x => Random.value).ToList();
        _drawPile = new Stack<Card>(shuffled.Select(d => new Card(d, null)));
        _discardPile.Clear();

        Debug.Log($"Deck initialized with {_drawPile.Count} cards");
    }

    /// <summary>
    /// Draw a card from the deck
    /// </summary>
    /// <returns>The drawn card, or null if deck is empty</returns>
    public Card Draw()
    {
        if (_drawPile.Count == 0)
        {
            Debug.LogWarning("Attempting to draw from empty deck!");
            return null;
        }

        return _drawPile.Pop();
    }

    /// <summary>
    /// Add a card to the discard pile
    /// </summary>
    public void Discard(Card card)
    {
        if (card == null)
        {
            Debug.LogWarning("Attempting to discard null card!");
            return;
        }

        _discardPile.Add(card);
        card.MarkAsPlayed();
    }

    /// <summary>
    /// Shuffle the discard pile back into the draw pile
    /// </summary>
    public void Shuffle()
    {
        if (_drawPile.Count == 0 && _discardPile.Count == 0)
        {
            Debug.LogError("Cannot shuffle an empty deck!");
            return;
        }

        var combined = _drawPile.Concat(_discardPile).OrderBy(x => Random.value).ToList();
        _drawPile = new Stack<Card>(combined);
        _discardPile.Clear();

        Debug.Log($"Deck shuffled. Draw pile: {_drawPile.Count} cards");
    }

    /// <summary>
    /// Reinsert a Puck'd card at a random position in the deck
    /// </summary>
    /// <param name="puckdCard">The Puck'd card to reinsert</param>
    /// <param name="index">The index to insert at (0 = top, Count-1 = bottom)</param>
    public void ReinsertPuckd(Card puckdCard, int index)
    {
        if (puckdCard?.Data.type != CardType.Puckd)
        {
            Debug.LogError("Can only reinsert Puck'd cards!");
            return;
        }

        var cards = _drawPile.ToList();
        index = Mathf.Clamp(index, 0, cards.Count);
        cards.Insert(index, puckdCard);
        _drawPile = new Stack<Card>(cards.AsEnumerable().Reverse());

        Debug.Log($"Reinserted Puck'd card at position {index}");
    }

    /// <summary>
    /// Peek at the top cards of the deck without drawing them
    /// </summary>
    /// <param name="count">Number of cards to peek at</param>
    /// <returns>List of cards from top of deck</returns>
    public List<Card> PeekTop(int count)
    {
        count = Mathf.Min(count, _drawPile.Count);
        return _drawPile.Take(count).ToList();
    }
}