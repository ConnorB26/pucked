using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a player in the game, managing their hand of cards and game state.
/// </summary>
public class Player
{
    #region Properties

    /// <summary>
    /// The player's display name
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The player's position in turn order (0-based)
    /// </summary>
    public int PlayerIndex { get; private set; }

    /// <summary>
    /// Whether the player has been eliminated by a Puck'd card
    /// </summary>
    public bool IsEliminated { get; private set; }

    /// <summary>
    /// The cards currently in the player's hand
    /// </summary>
    public IReadOnlyList<Card> Hand => _hand;

    private readonly List<Card> _hand = new();

    /// <summary>
    /// Reference to the UI element representing this player
    /// </summary>
    public PlayerUI PlayerUI { get; set; }

    /// <summary>
    /// Number of cards currently in hand
    /// </summary>
    public int HandSize => _hand.Count;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new player with the specified name and turn order
    /// </summary>
    /// <param name="name">Player's display name</param>
    /// <param name="index">Player's position in turn order (0-based)</param>
    public Player(string name, int index)
    {
        Name = name;
        PlayerIndex = index;
        IsEliminated = false;
    }

    #endregion

    #region Card Management

    /// <summary>
    /// Draw a card from the deck and add it to the player's hand
    /// </summary>
    /// <param name="deck">The deck to draw from</param>
    /// <returns>True if card was successfully drawn, false if deck is empty</returns>
    public bool DrawCard(Deck deck)
    {
        if (IsEliminated)
        {
            Debug.LogWarning($"Eliminated player {Name} cannot draw cards!");
            return false;
        }

        var card = deck.Draw();
        if (card == null) return false;

        AddCardToHand(card);
        Debug.Log($"{Name} drew {card.Data.cardName}");
        return true;
    }

    /// <summary>
    /// Add a specific card to the player's hand
    /// </summary>
    internal void AddCardToHand(Card card)
    {
        if (card == null)
        {
            Debug.LogError("Attempting to add null card to hand!");
            return;
        }

        _hand.Add(card);
        card.Owner = this;
        UpdateUI();
    }

    /// <summary>
    /// Remove a specific card from the player's hand
    /// </summary>
    internal void RemoveCardFromHand(Card card)
    {
        if (!_hand.Contains(card))
        {
            Debug.LogWarning($"Card {card.Data.cardName} not found in {Name}'s hand!");
            return;
        }

        _hand.Remove(card);
        UpdateUI();
    }

    /// <summary>
    /// Play a card from the player's hand
    /// </summary>
    /// <param name="card">The card to play</param>
    /// <param name="game">Current game instance</param>
    /// <param name="targetPlayer">Optional target player for cards that need targets</param>
    public void PlayCard(Card card, GameManager game, Player targetPlayer = null)
    {
        if (!_hand.Contains(card))
        {
            Debug.LogWarning($"Cannot play card {card.Data.cardName} - not in {Name}'s hand!");
            return;
        }

        if (IsEliminated)
        {
            Debug.LogWarning($"Eliminated player {Name} cannot play cards!");
            return;
        }

        game.CardHandler.HandleCardPlay(card, this, targetPlayer);
    }

    /// <summary>
    /// Attempt to use a Save card to block a Puck'd
    /// </summary>
    /// <returns>True if save was successful, false if no save card available</returns>
    public bool TryUseSaveCard()
    {
        if (IsEliminated)
        {
            Debug.LogWarning($"Eliminated player {Name} cannot use save cards!");
            return false;
        }

        var save = _hand.FirstOrDefault(c => c.Data.type == CardType.Save);
        if (save == null)
        {
            Debug.Log($"{Name} has no save cards!");
            return false;
        }

        RemoveCardFromHand(save);
        Debug.Log($"{Name} used {save.Data.cardName} to block Puck'd!");
        return true;
    }

    #endregion

    #region Game State

    /// <summary>
    /// Eliminate the player from the game (usually from drawing a Puck'd card)
    /// </summary>
    public void Eliminate()
    {
        if (IsEliminated) return;

        IsEliminated = true;
        // Discard hand when eliminated
        _hand.Clear();
        Debug.Log($"{Name} has been Puck'd and eliminated!");
        UpdateUI();
    }

    /// <summary>
    /// Check if the player has any cards of the specified type
    /// </summary>
    public bool HasCardOfType(CardType type)
    {
        return _hand.Any(c => c.Data.type == type);
    }

    #endregion

    #region UI

    /// <summary>
    /// Update the UI to reflect current hand state
    /// </summary>
    public void UpdateUI()
    {
        //PlayerUI?.RefreshHand(Hand);
    }

    public override string ToString()
    {
        return $"{Name} (Cards: {HandSize}, {(IsEliminated ? "Eliminated" : "Active")})";
    }

    #endregion
}