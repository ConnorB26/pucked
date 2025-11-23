using Actions;
using Effects.Base;
using Resolver;
using UnityEngine;

namespace Effects.Implementations
{
    [CreateAssetMenu(menuName = "Puckd/Effects/Peek")]
    public class PeekEffect : CardEffect
    {
        public int peekAmount = 3;

        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.PeekCards,
                INTPayload = peekAmount
            };
        }
    }
}