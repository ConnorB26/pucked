using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Deck
{
    private Stack<Card> _drawPile = new();
    private readonly List<Card> _discardPile = new();

    public void Initialize(List<CardData> allCardData)
    {
        // Shuffle incoming CardData and create Card instances
        var shuffled = allCardData.OrderBy(x => Random.value).ToList();
        _drawPile = new Stack<Card>(shuffled.Select(d => new Card(d, null)));
    }

    public Card Draw()
    {
        if (_drawPile.Count == 0) return null;
        return _drawPile.Pop();
    }

    public void Discard(Card card) => _discardPile.Add(card);

    public void Shuffle()
    {
        var combined = _drawPile.Concat(_discardPile).OrderBy(x => Random.value).ToList();
        _drawPile = new Stack<Card>(combined);
        _discardPile.Clear();
    }

    public void ReinsertPuckd(Card puckdCard, int index)
    {
        var cards = _drawPile.ToList();
        cards.Insert(index, puckdCard);
        _drawPile = new Stack<Card>(cards.AsEnumerable().Reverse());
    }

    public int CountRemaining()
    {
        return _drawPile.Count;
    }
}