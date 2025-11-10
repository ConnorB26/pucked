using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles the execution of card effects in the game.
/// This class is responsible for processing card plays and managing their effects.
/// </summary>
public class CardActionHandler
{
    /// <summary>
    /// Event fired when a player needs to see cards from a Peek action.
    /// Subscribers (usually UI) should display these cards to the specific player.
    /// </summary>
    public event Action<Player, CardData[]> OnPeekCards;

    /// <summary>
    /// Process a card being played by a player
    /// </summary>
    /// <param name="card">The card being played</param>
    /// <param name="player">The player playing the card</param>
    /// <param name="targetPlayer">Optional target player for cards that need targets</param>
    public void HandleCardPlay(Card card, Player player, Player targetPlayer = null)
    {
        if (!card.CanPlay(targetPlayer))
        {
            Debug.LogWarning($"Card {card.Data.cardName} cannot be played right now!");
            return;
        }

        if (!player.Hand.Contains(card))
        {
            Debug.LogWarning($"Player {player.Name} doesn't have card {card.Data.cardName} in hand!");
            return;
        }

        // Process the card effect
        var wasSuccessful = ProcessCardEffect(card, player, targetPlayer);
        if (!wasSuccessful) return;
        
        // Remove from hand and mark as played
        player.RemoveCardFromHand(card);
        card.MarkAsPlayed();
        GameManager.Instance.Deck.Discard(card);
        player.UpdateUI();

        Debug.Log($"{player.Name} successfully played {card.Data.cardName}");
    }

    private bool ProcessCardEffect(Card card, Player player, Player targetPlayer)
    {
        try
        {
            switch (card.Data.type)
            {
                case CardType.Puckd:
                    return HandlePuckd(card, player);
                case CardType.Save:
                    return HandleSave(card, player);
                case CardType.Cancel:
                    return HandleCancel(card, player);
                case CardType.Attack:
                    return HandleAttack(card, player, targetPlayer);
                case CardType.Skip:
                    return HandleSkip(card, player);
                case CardType.Peek:
                    return HandlePeek(card, player);
                case CardType.Shuffle:
                    return HandleShuffle(card, player);
                default:
                    Debug.LogError($"Unknown card type: {card.Data.type}");
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing card effect: {e.Message}");
            return false;
        }
    }

    private bool HandlePuckd(Card card, Player player)
    {
        // Puck'd cards are handled when drawn, not played
        Debug.LogWarning("Puck'd cards cannot be played directly!");
        return false;
    }

    private bool HandleSave(Card card, Player player)
    {
        // Save cards are handled reactively when Puck'd is drawn
        Debug.Log($"{player.Name} readied {card.Data.cardName} to block the next Puck'd!");
        return true;
    }

    private bool HandleCancel(Card card, Player player)
    {
        // TODO: Add last action tracking system
        Debug.Log($"{player.Name} played {card.Data.cardName} - Cancelling last action!");
        return true;
    }

    private bool HandleAttack(Card card, Player player, Player targetPlayer)
    {
        if (targetPlayer == null || targetPlayer.IsEliminated)
        {
            Debug.LogWarning("Invalid target for Attack card!");
            return false;
        }

        Debug.Log(
            $"{player.Name} played {card.Data.cardName} - {targetPlayer.Name} must take {card.Data.extraTurns} extra turns!");
        GameManager.Instance.AddExtraTurns(card.Data.extraTurns);
        return true;
    }

    private bool HandleSkip(Card card, Player player)
    {
        Debug.Log($"{player.Name} played {card.Data.cardName} - Skipping turn!");
        GameManager.Instance.EndTurn();
        return true;
    }

    private bool HandlePeek(Card card, Player player)
    {
        var peekAmount = card.Data.peekAmount;
        var topCards = GameManager.Instance.Deck.PeekTop(peekAmount);

        if (topCards.Count == 0)
        {
            Debug.LogWarning("No cards to peek at!");
            return false;
        }

        Debug.Log($"{player.Name} played {card.Data.cardName} - Peeking at top {topCards.Count} cards");
        // Convert to CardData array since we only need the static data for display
        var cardDataToShow = topCards.Select(c => c.Data).ToArray();
        OnPeekCards?.Invoke(player, cardDataToShow);
        return true;
    }

    private bool HandleShuffle(Card card, Player player)
    {
        Debug.Log($"{player.Name} played {card.Data.cardName} - Shuffling the deck!");
        GameManager.Instance.Deck.Shuffle();
        return true;
    }
}