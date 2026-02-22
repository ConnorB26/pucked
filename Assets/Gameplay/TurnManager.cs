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

        public int CurrentPlayerId => _players[_currentIndex].PlayerId;

        public TurnManager(List<PlayerRuntime> players)
        {
            _players = players;
            _currentIndex = 0;
        }

        /// <summary>Call when the current player finishes their turn normally (all draws done).</summary>
        public void EndTurn()
        {
            AdvanceToNextAlivePlayer();
        }

        /// <summary>
        /// Skip one pending extra turn for the current player (e.g. playing a Skip card while
        /// under Attack). If extra turns are still owed the player stays active; otherwise the
        /// turn advances to the next player.
        /// </summary>
        public void SkipCurrentPlayer()
        {
            var current = _players[_currentIndex];
            if (current.PendingExtraTurns > 0)
            {
                current.PendingExtraTurns--;
                // Player still owes draws — they remain active until PendingExtraTurns == 0
                // and they click End Turn (which draws everything in one shot).
                if (current.PendingExtraTurns > 0)
                    return;
            }

            AdvanceToNextAlivePlayer();
        }

        /// <summary>
        /// Immediately sets the active turn to a specific player (e.g. targeted Attack).
        /// Falls back to the next alive player if the target is eliminated.
        /// </summary>
        public void JumpToPlayer(int playerId)
        {
            var idx = _players.FindIndex(p => p.PlayerId == playerId && !p.IsEliminated);
            if (idx >= 0)
                _currentIndex = idx;
            else
                AdvanceToNextAlivePlayer();
        }

        public void OnPlayerEliminated(int playerId)
        {
            // Nothing fancy for now; if the eliminated player was current,
            // advance to the next alive player.
            var idx = _players.FindIndex(p => p.PlayerId == playerId);
            if (idx < 0) return;

            _players[idx].IsEliminated = true;

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
                if (!_players[_currentIndex].IsEliminated)
                    return;
            } while (_currentIndex != start);

            // If we looped and found nobody, all players are eliminated.
            Debug.LogWarning("TurnManager: all players appear eliminated.");
        }
    }
}