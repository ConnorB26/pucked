using UnityEngine;

/// <summary>
/// Represents a card type and how many copies should be in the deck.
/// Used by GameManager to define the card pool.
/// </summary>
[System.Serializable]
public class CardEntry
{
    [Tooltip("The card definition to include")]
    public CardData cardData;

    [Tooltip("How many copies of this card should be in the deck")] [Range(1, 20)]
    public int count = 1;
}