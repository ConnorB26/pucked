using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player
{
    public string Name { get; private set; }
    public int PlayerIndex { get; private set; }
    public bool IsEliminated { get; private set; }

    public List<Card> Hand { get; private set; } = new();

    // You can assign this from GameManager to link to a UI element
    public PlayerUI PlayerUI { get; set; }

    public Player(string name, int index)
    {
        Name = name;
        PlayerIndex = index;
        IsEliminated = false;
    }

    // Draw a new card from the deck
    public void DrawCard(Deck deck)
    {
        var card = deck.Draw();
        Hand.Add(card);
        card.Owner = this;
        UpdateUI();
        Debug.Log($"{Name} drew {card.Data.cardName}");
    }

    // Play a card
    public void PlayCard(Card card, GameManager game)
    {
        if (!Hand.Contains(card)) return;

        Hand.Remove(card);
        card.Play(game);
        UpdateUI();
    }

    // Use save card (defuse)
    public bool TryUseSaveCard()
    {
        var save = Hand.FirstOrDefault(c => c.Data.type == CardType.Save);
        if (save == null) return false;

        Hand.Remove(save);
        Debug.Log($"{Name} used {save.Data.cardName}!");
        UpdateUI();
        return true;
    }

    // Eliminate player (no save available)
    public void Eliminate()
    {
        IsEliminated = true;
        Debug.Log($"{Name} has been Puck’d and eliminated!");
        UpdateUI();
    }

    // UI sync helper
    private void UpdateUI()
    {
        PlayerUI?.RefreshHand(Hand);
    }
}