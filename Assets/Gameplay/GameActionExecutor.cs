using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Takes resolved GameAction instances and mutates GameContext accordingly.
    /// UI/network hook points are exposed as events.
    /// </summary>
    public class GameActionExecutor
    {
        private readonly GameContext _ctx;

        public delegate void PeekHandler(int playerId, List<CardDefinition> cards);

        public event PeekHandler OnPeekRequested;

        public delegate void EliminationHandler(int playerId);

        public event EliminationHandler OnPlayerEliminated;

        public GameActionExecutor(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void ApplyActions(List<GameAction> actions)
        {
            foreach (var action in actions)
                Apply(action);
        }

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
                    Debug.Log("PreventElimination action received - handled in resolver / stack logic.");
                    break;

                case ActionType.ForceExtraTurns:
                    // Attack ends the attacker's turn immediately (they don't draw).
                    // Jump directly to the targeted player if one was specified; otherwise
                    // advance to the next player in rotation.
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

        private void HandleEliminationRequest(int ownerPlayerId, int targetPlayerId)
        {
            // For now, target == owner (Puck'd like Exploding Kittens).
            var victimId = targetPlayerId == 0 ? ownerPlayerId : targetPlayerId;

            var player = _ctx.GetPlayer(victimId);
            if (player == null || player.IsEliminated)
                return;

            player.IsEliminated = true;
            _ctx.TurnManager.OnPlayerEliminated(victimId);

            // Optionally discard hand
            if (_ctx.Config.discardHandOnElimination && player.Hand.Count > 0)
            {
                // Discard actual instances
                _ctx.DeckManager.DiscardMany(player.Hand);
                player.Hand.Clear();
            }

            OnPlayerEliminated?.Invoke(victimId);
        }

        private void HandlePeek(int playerId, int count)
        {
            // Peek top instances
            var instances = _ctx.DeckManager.PeekTop(count);

            // UI probably only cares about CardDefinition here
            var defs = instances
                .Where(ci => ci.Definition != null)
                .Select(ci => ci.Definition)
                .ToList();

            Debug.Log($"Player {playerId} peeks {defs.Count} cards.");
            OnPeekRequested?.Invoke(playerId, defs);
        }
    }
}