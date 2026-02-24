using System.Collections.Generic;
using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// Stack-based effect resolver. Server-only MonoBehaviour.
    /// Effects pushed by QueueEffects are popped LIFO and converted to GameActions by ResolveAll.
    /// </summary>
    public class EffectResolver : MonoBehaviour
    {
        private readonly Stack<PendingEffect> _stack = new();

        /// <summary>Pushes all effects from a card play onto the resolution stack.</summary>
        public void QueueEffects(List<CardEffect> effects, EffectContext context)
        {
            foreach (var fx in effects)
                _stack.Push(fx.CreateRuntimeEffect(context));
        }

        /// <summary>
        /// Pops and resolves all queued effects into a list of GameActions.
        /// CancelLastEffect removes the previous output rather than adding a new action.
        /// </summary>
        public List<GameAction> ResolveAll()
        {
            List<GameAction> output = new();

            while (_stack.Count > 0)
            {
                var fx = _stack.Pop();

                switch (fx.ActionType)
                {
                    case ActionType.CancelLastEffect:
                        if (output.Count > 0)
                            output.RemoveAt(output.Count - 1);
                        break;

                    case ActionType.RequestElimination:
                    case ActionType.PreventElimination:
                    case ActionType.ForceExtraTurns:
                    case ActionType.SkipTurn:
                    case ActionType.PeekCards:
                    case ActionType.ShuffleDeck:
                    default:
                        output.Add(new GameAction(fx.ActionType, fx.INTPayload, fx.Context));
                        break;
                }
            }

            return output;
        }
    }
}
