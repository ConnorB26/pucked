using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Cancels the most recently resolved effect, removing its action from the output list.</summary>
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
