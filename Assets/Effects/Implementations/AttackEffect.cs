using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Forces the target player to take extra turns, drawing cards at the start of each.</summary>
    [CreateAssetMenu(menuName = "Puckd/Effects/Attack")]
    public class AttackEffect : CardEffect
    {
        [Tooltip("How many extra turns the target player must take.")]
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
