using System.Collections.Generic;

namespace Gameplay
{
    /// <summary>
    /// Container passed around to components that need to modify core game state.
    /// </summary>
    public class GameContext
    {
        public GameConfig Config;
        public DeckManager DeckManager;
        public TurnManager TurnManager;
        public List<PlayerRuntime> Players;

        public GameContext(GameConfig config,
            DeckManager deckManager,
            TurnManager turnManager,
            List<PlayerRuntime> players)
        {
            this.Config = config;
            this.DeckManager = deckManager;
            this.TurnManager = turnManager;
            this.Players = players;
        }

        public PlayerRuntime GetPlayer(int playerId)
            => Players.Find(p => p.PlayerId == playerId);
    }
}