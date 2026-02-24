using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Applies resolved <see cref="GameAction"/> instances to <see cref="GameContext"/>.
    /// Exposes events for networking and UI hooks.
    /// </summary>
    public class GameActionExecutor
    {
        #region Fields & Events

        private readonly GameContext _ctx;

        public delegate void PeekHandler(int playerId, List<CardDefinition> cards);
        public delegate void EliminationHandler(int playerId);

        public event PeekHandler OnPeekRequested;
        public event EliminationHandler OnPlayerEliminated;

        #endregion

        public GameActionExecutor(GameContext ctx) => _ctx = ctx;

        #region Public API

        /// <summary>Applies each action in sequence.</summary>
        public void ApplyActions(List<GameAction> actions)
        {
            foreach (var action in actions)
                Apply(action);
        }

        #endregion

        #region Action Dispatch

        private void Apply(GameAction action)
        {
            var type = action.Type;
            var value = action.Value;
            var context = action.Context;

            switch (type)
            {
                case ActionType.RequestElimination:
                    HandleEliminationRequest(context.OwnerPlayerId, context.TargetPlayerId);
                    break;

                case ActionType.PreventElimination:
                    break;

                case ActionType.ForceExtraTurns:
                    // Jump to targeted player if specified, otherwise advance to next in rotation.
                    if (context.TargetPlayerId != 0 && context.TargetPlayerId != context.OwnerPlayerId)
                        _ctx.TurnManager.JumpToPlayer(context.TargetPlayerId);
                    else
                        _ctx.TurnManager.SkipCurrentPlayer();

                    var victim = _ctx.GetPlayer(_ctx.TurnManager.CurrentPlayerId);
                    if (victim != null)
                        victim.PendingExtraTurns += value;
                    break;

                case ActionType.SkipTurn:
                    _ctx.TurnManager.SkipCurrentPlayer();
                    break;

                case ActionType.PeekCards:
                    HandlePeek(context.OwnerPlayerId, value);
                    break;

                case ActionType.ShuffleDeck:
                    _ctx.DeckManager.Shuffle();
                    break;

                case ActionType.CancelLastEffect:
                default:
                    Debug.LogWarning($"Unhandled GameAction type: {type}");
                    break;
            }
        }

        #endregion

        #region Handlers

        private void HandleEliminationRequest(int ownerPlayerId, int targetPlayerId)
        {
            var victimId = targetPlayerId == 0 ? ownerPlayerId : targetPlayerId;

            var player = _ctx.GetPlayer(victimId);
            if (player == null || player.IsEliminated) return;

            player.IsEliminated = true;
            _ctx.TurnManager.OnPlayerEliminated(victimId);

            if (_ctx.Config.discardHandOnElimination && player.Hand.Count > 0)
            {
                _ctx.DeckManager.DiscardMany(player.Hand);
                player.Hand.Clear();
            }

            OnPlayerEliminated?.Invoke(victimId);
        }

        private void HandlePeek(int playerId, int count)
        {
            var instances = _ctx.DeckManager.PeekTop(count);
            var defs = instances
                .Where(ci => ci.Definition != null)
                .Select(ci => ci.Definition)
                .ToList();

            OnPeekRequested?.Invoke(playerId, defs);
        }

        #endregion
    }
}
