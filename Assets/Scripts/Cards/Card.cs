using UnityEngine;

/// <summary>
/// Represents a playable card in the game. This class manages the runtime state of a card,
/// while CardData contains the static configuration.
/// </summary>
public class Card
{
    /// <summary>
    /// The static data/configuration for this card
    /// </summary>
    public CardData Data { get; }

    /// <summary>
    /// The current owner/holder of this card
    /// </summary>
    public Player Owner { get; set; }

    /// <summary>
    /// Whether this card has been played and should be discarded
    /// </summary>
    public bool HasBeenPlayed { get; private set; }

    public Card(CardData cardData, Player cardOwner)
    {
        Data = cardData;
        Owner = cardOwner;
        HasBeenPlayed = false;
    }

    /// <summary>
    /// Checks if this card can be played in the current game state
    /// </summary>
    /// <param name="targetPlayer">The player being targeted, if this is a targeted card</param>
    /// <returns>True if the card can be played, false otherwise</returns>
    public virtual bool CanPlay(Player targetPlayer = null)
    {
        if (HasBeenPlayed)
        {
            Debug.LogWarning($"Card {Data.cardName} has already been played!");
            return false;
        }

        if (Owner == null)
        {
            Debug.LogWarning($"Card {Data.cardName} has no owner!");
            return false;
        }

        if (Owner.IsEliminated)
        {
            Debug.LogWarning($"Eliminated player {Owner.Name} cannot play cards!");
            return false;
        }

        switch (Data.RequiresTarget)
        {
            case true when targetPlayer == null:
                Debug.LogWarning($"Card {Data.cardName} requires a target player!");
                return false;
            case true when targetPlayer.IsEliminated:
                Debug.LogWarning($"Cannot target eliminated player {targetPlayer.Name}!");
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Mark this card as played - called by CardActionHandler after successful play
    /// </summary>
    internal void MarkAsPlayed()
    {
        HasBeenPlayed = true;
    }

    public override string ToString()
    {
        return $"{Data.cardName} ({Data.type})";
    }
}