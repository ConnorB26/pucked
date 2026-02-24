using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Skips the owner's draw at end of turn. Consumes one pending extra turn if the player has any.</summary>
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
