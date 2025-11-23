using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    [CreateAssetMenu(menuName = "Puckd/Effects/Skip Turn")]
    public class SkipEffect : CardEffect
    {
        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.SkipTurn
            };
        }
    }
}