using System.Collections.Generic;
using UnityEngine;

public class TestGameSetup : MonoBehaviour
{
    [Tooltip("Reference to GameManager in the scene")]
    public GameManager gameManager;

    [Tooltip("Number of test players to spawn")] [Range(2, 6)]
    public int playerCount = 4;

    void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError("GameManager not assigned in TestGameSetup!");
            return;
        }

        // Hardcode player names for testing
        var testPlayers = new List<string>();
        for (var i = 0; i < playerCount; i++)
        {
            testPlayers.Add($"Player {i + 1}");
        }

        // Start the game
        gameManager.StartGame(testPlayers);
    }
}