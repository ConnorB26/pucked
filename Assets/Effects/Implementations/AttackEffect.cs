using Actions;
using Effects.Base;
using Resolver;
using UnityEngine;

namespace Effects.Implementations
{
    [CreateAssetMenu(menuName = "Puckd/Effects/Attack")]
    public class AttackEffect : CardEffect
    {
        [Tooltip("How many extra turns the next player must take.")]
        public int extraTurns = 2;

        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.ForceExtraTurns,
                INTPayload = extraTurns
            };
        }
    }
}