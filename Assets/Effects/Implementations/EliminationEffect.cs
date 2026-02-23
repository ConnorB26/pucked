using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Requests elimination of the card owner. Used internally by the Puck'd draw logic.</summary>
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
