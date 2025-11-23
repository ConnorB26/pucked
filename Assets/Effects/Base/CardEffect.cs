using UnityEngine;

namespace Effects.Base
{
    /// <summary>
    /// Base ScriptableObject describing an effect.
    /// Does NOT execute game logic directly.
    /// </summary>
    public abstract class CardEffect : ScriptableObject
    {
        [TextArea] public string description;

        /// <summary>
        /// Converts this effect into a PendingEffect
        /// that the resolver can push onto the stack.
        /// </summary>
        public abstract PendingEffect CreateRuntimeEffect(EffectContext context);
    }
}