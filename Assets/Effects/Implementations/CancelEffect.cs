using Actions;
using Effects.Base;
using Resolver;
using UnityEngine;

namespace Effects.Implementations
{
    [CreateAssetMenu(menuName = "Puckd/Effects/Cancel")]
    public class CancelEffect : CardEffect
    {
        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.CancelLastEffect
            };
        }
    }
}