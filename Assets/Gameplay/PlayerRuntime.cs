using System.Collections.Generic;
using Cards;

namespace Gameplay
{
    /// <summary>
    /// Runtime state for a single player in the match.
    /// </summary>
    public class PlayerRuntime
    {
        public readonly int PlayerId;

        public bool IsEliminated;

        public readonly List<CardInstance> Hand = new();

        public PlayerRuntime(int id)
        {
            PlayerId = id;
        }
    }
}