using System.Collections.Generic;
using Cards;

namespace Gameplay
{
    /// <summary>
    /// Runtime state for a single player in the match.
    /// </summary>
    public class PlayerRuntime
    {
        public int playerId; // matches whatever ID you use elsewhere
        public string displayName;

        public bool isEliminated;

        public List<CardInstance> hand = new();

        public PlayerRuntime(int id, string name)
        {
            playerId = id;
            displayName = name;
        }
    }
}