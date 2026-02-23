using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Lets the owner see the top N cards of the draw pile without drawing them.</summary>
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
