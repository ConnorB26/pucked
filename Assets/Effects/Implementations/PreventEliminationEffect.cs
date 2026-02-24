using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Blocks the next elimination. Auto-consumed when Goalie Save triggers on a Puck'd draw.</summary>
    [CreateAssetMenu(menuName = "Puckd/Effects/Prevent Elimination")]
    public class PreventEliminationEffect : CardEffect
    {
        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.PreventElimination
            };
        }
    }
}
