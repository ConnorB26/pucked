using Actions;
using Effects.Base;
using UnityEngine;

namespace Effects.Implementations
{
    /// <summary>Shuffles the draw pile using Fisher-Yates.</summary>
    [CreateAssetMenu(menuName = "Puckd/Effects/Shuffle")]
    public class ShuffleEffect : CardEffect
    {
        public override PendingEffect CreateRuntimeEffect(EffectContext context)
        {
            return new PendingEffect
            {
                Context = context,
                Effect = this,
                ActionType = ActionType.ShuffleDeck
            };
        }
    }
}
