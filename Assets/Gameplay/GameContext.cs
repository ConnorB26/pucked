using System.Collections.Generic;

namespace Gameplay
{
    /// <summary>
    /// Container passed around to components that need to modify core game state.
    /// </summary>
    public class GameContext
    {
        public GameConfig config;
        public DeckManager deckManager;
        public TurnManager turnManager;
        public List<PlayerRuntime> players;

        public GameContext(GameConfig config,
            DeckManager deckManager,
            TurnManager turnManager,
            List<PlayerRuntime> players)
        {
            this.config = config;
            this.deckManager = deckManager;
            this.turnManager = turnManager;
            this.players = players;
        }

        public PlayerRuntime GetPlayer(int playerId)
            => players.Find(p => p.playerId == playerId);
    }
}