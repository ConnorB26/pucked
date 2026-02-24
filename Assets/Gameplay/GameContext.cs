using System.Collections.Generic;

namespace Gameplay
{
    /// <summary>
    /// Container passed around to components that need to modify core game state.
    /// </summary>
    public class GameContext
    {
        public readonly GameConfig Config;
        public readonly DeckManager DeckManager;
        public readonly TurnManager TurnManager;
        public readonly List<PlayerRuntime> Players;

        public GameContext(GameConfig config,
            DeckManager deckManager,
            TurnManager turnManager,
            List<PlayerRuntime> players)
        {
            Config = config;
            DeckManager = deckManager;
            TurnManager = turnManager;
            Players = players;
        }

        public PlayerRuntime GetPlayer(int playerId)
            => Players.Find(p => p.PlayerId == playerId);
    }
}