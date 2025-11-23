using System.Collections.Generic;
using Actions;
using Effects.Base;
using UnityEngine;

namespace Resolver
{
    /// <summary>
    /// Manages execution order of effects.
    /// Handles canceling, prevents, etc.
    /// Only server should run this.
    /// </summary>
    public class EffectResolver : MonoBehaviour
    {
        private readonly Stack<PendingEffect> _stack = new();

        public void QueueEffects(List<CardEffect> effects, EffectContext context)
        {
            foreach (var fx in effects)
                _stack.Push(fx.CreateRuntimeEffect(context));
        }

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

                    default:
                        output.Add(new GameAction(fx.ActionType, fx.INTPayload, fx.Context));
                        break;
                }
            }

            return output;
        }
    }
}