using Actions;
using Effects.Base;
using Resolver;
using UnityEngine;

namespace Effects.Implementations
{
    [CreateAssetMenu(menuName = "Puckd/Effects/Elimination")]
    public class EliminationEffect : CardEffect
    {
        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.RequestElimination
            };
        }
    }
}