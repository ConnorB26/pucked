using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Handles turn order, skips, extra turns from Attack cards, and elimination.
    /// </summary>
    public class TurnManager
    {
        private readonly List<PlayerRuntime> _players;
        private int _currentIndex;
        private int _pendingExtraTurns;

        public int CurrentPlayerId => _players[_currentIndex].playerId;

        public TurnManager(List<PlayerRuntime> players)
        {
            _players = players;
            _currentIndex = 0;
            _pendingExtraTurns = 0;
        }

        /// <summary>Call when the current player finishes their turn normally.</summary>
        public void EndTurn()
        {
            if (_pendingExtraTurns > 0)
            {
                _pendingExtraTurns--;
                // same player again
                return;
            }

            AdvanceToNextAlivePlayer();
        }

        /// <summary>Skip the current player's remaining turns and go to the next.</summary>
        public void SkipCurrentPlayer()
        {
            AdvanceToNextAlivePlayer();
        }

        /// <summary>Force the NEXT player in order to take 'turns' extra turns.</summary>
        public void AddExtraTurnsForNextPlayer(int turns)
        {
            // For now we model this as "when we move to the next player, they get extra turns".
            _pendingExtraTurns += Mathf.Max(0, turns);
        }

        public void OnPlayerEliminated(int playerId)
        {
            // Nothing fancy for now; if the eliminated player was current,
            // advance to the next alive player.
            var idx = _players.FindIndex(p => p.playerId == playerId);
            if (idx < 0) return;

            _players[idx].isEliminated = true;

            if (idx == _currentIndex)
            {
                AdvanceToNextAlivePlayer();
            }
        }

        private void AdvanceToNextAlivePlayer()
        {
            if (_players.Count == 0)
                return;

            var start = _currentIndex;
            do
            {
                _currentIndex = (_currentIndex + 1) % _players.Count;
                if (!_players[_currentIndex].isEliminated)
                    return;
            } while (_currentIndex != start);

            // If we looped and found nobody, all players are eliminated.
            Debug.LogWarning("TurnManager: all players appear eliminated.");
        }
    }
}