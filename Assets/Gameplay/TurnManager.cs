using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Manages turn order, skips, extra turns from attacks, and eliminated-player advancement.
    /// </summary>
    public class TurnManager
    {
        #region Fields

        private readonly List<PlayerRuntime> _players;
        private int _currentIndex;

        #endregion

        #region Properties

        public int CurrentPlayerId => _players[_currentIndex].PlayerId;

        #endregion

        public TurnManager(List<PlayerRuntime> players)
        {
            _players = players;
            _currentIndex = 0;
        }

        #region Turn Advancement

        /// <summary>Advances to the next alive player. Called after draws are complete.</summary>
        public void EndTurn()
        {
            AdvanceToNextAlivePlayer();
        }

        /// <summary>
        /// Consumes one pending extra turn if owed (e.g. Skip while under Attack),
        /// only advancing once all extra turns are cleared.
        /// </summary>
        public void SkipCurrentPlayer()
        {
            var current = _players[_currentIndex];
            if (current.PendingExtraTurns > 0)
            {
                current.PendingExtraTurns--;
                if (current.PendingExtraTurns > 0)
                    return;
            }

            AdvanceToNextAlivePlayer();
        }

        /// <summary>Sets the active turn to a specific player (targeted Attack). Falls back if target is eliminated.</summary>
        public void JumpToPlayer(int playerId)
        {
            var idx = _players.FindIndex(p => p.PlayerId == playerId && !p.IsEliminated);
            if (idx >= 0)
                _currentIndex = idx;
            else
                AdvanceToNextAlivePlayer();
        }

        #endregion

        #region Elimination

        /// <summary>Marks a player eliminated and advances the turn if they were current.</summary>
        public void OnPlayerEliminated(int playerId)
        {
            var idx = _players.FindIndex(p => p.PlayerId == playerId);
            if (idx < 0) return;

            _players[idx].IsEliminated = true;

            if (idx == _currentIndex)
                AdvanceToNextAlivePlayer();
        }

        #endregion

        #region Helpers

        private void AdvanceToNextAlivePlayer()
        {
            if (_players.Count == 0) return;

            var start = _currentIndex;
            do
            {
                _currentIndex = (_currentIndex + 1) % _players.Count;
                if (!_players[_currentIndex].IsEliminated)
                    return;
            } while (_currentIndex != start);

            Debug.LogWarning("TurnManager: all players appear eliminated.");
        }

        #endregion
    }
}
