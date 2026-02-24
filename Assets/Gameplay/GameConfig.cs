using Cards;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// High-level configuration for a match of Puck'd.
    /// This is chosen in the editor and used at runtime by the GameManager.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Puckd/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Tooltip("Number of cards each player draws at game start.")] [Min(0)]
        public int startingHandSize = 5;

        [Header("Deck / Cards")] [Tooltip("Deck definition to use for this game mode.")]
        public DeckDefinition deckDefinition;

        [Tooltip("Whether players automatically draw 1 card at the end of their turn (Exploding Kittens-style).")]
        public bool drawAtEndOfTurn = true;

        [Min(0)] [Tooltip("If drawAtEndOfTurn is true, how many cards to draw.")]
        public int drawPerTurn = 1;

        [Header("Puck'd Rules")] [Tooltip("If enabled, the game ends immediately when only one player is left.")]
        public bool lastPlayerStandingWins = true;

        [Tooltip("Whether eliminated players keep their hand in place (for future mechanics) or discard it.")]
        public bool discardHandOnElimination = true;

        [Header("Debug / Dev")] [Tooltip("If true, shuffle is skipped and deck is used in defined order.")]
        public bool disableShuffle;
    }
}