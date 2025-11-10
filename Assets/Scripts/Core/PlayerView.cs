using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Serializable view of Player data for the Unity Inspector
/// </summary>
[System.Serializable]
public class PlayerView
{
    [SerializeField] private string playerName;
    [SerializeField] private int cardCount;
    [SerializeField] private bool isEliminated;
    [SerializeField] private List<string> cardNames = new();

    public static PlayerView FromPlayer(Player player)
    {
        return new PlayerView
        {
            playerName = player.Name,
            cardCount = player.HandSize,
            isEliminated = player.IsEliminated,
            cardNames = player.Hand.Select(c => c.Data.cardName).ToList()
        };
    }
}
