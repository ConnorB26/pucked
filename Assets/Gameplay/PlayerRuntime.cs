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

        /// <summary>
        /// Extra draw-turns owed (e.g. from an Attack card). Each unit means one additional
        /// drawPerTurn worth of cards must be drawn at end of turn.
        /// </summary>
        public int PendingExtraTurns;

        public readonly List<CardInstance> Hand = new();

        public PlayerRuntime(int id)
        {
            PlayerId = id;
        }
    }
}