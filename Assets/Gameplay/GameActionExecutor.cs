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
                    _ctx.turnManager.AddExtraTurnsForNextPlayer(value);
                    break;

                case ActionType.SkipTurn:
                    _ctx.turnManager.SkipCurrentPlayer();
                    break;

                case ActionType.PeekCards:
                    HandlePeek(context.OwnerPlayerId, value);
                    break;

                case ActionType.ShuffleDeck:
                    _ctx.deckManager.Shuffle();
                    break;

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
            if (player == null || player.isEliminated)
                return;

            player.isEliminated = true;
            _ctx.turnManager.OnPlayerEliminated(victimId);

            // Optionally discard hand
            if (_ctx.config.discardHandOnElimination && player.hand.Count > 0)
            {
                // Discard actual instances
                _ctx.deckManager.DiscardMany(player.hand);
                player.hand.Clear();
            }

            OnPlayerEliminated?.Invoke(victimId);
        }

        private void HandlePeek(int playerId, int count)
        {
            // Peek top instances
            var instances = _ctx.deckManager.PeekTop(count);

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