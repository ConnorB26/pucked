using UnityEngine;

namespace Effects.Base
{
    /// <summary>
    /// Abstract ScriptableObject describing a card effect. Does not execute game logic directly —
    /// it produces a PendingEffect that the EffectResolver resolves into a GameAction.
    /// </summary>
    public abstract class CardEffect : ScriptableObject
    {
        [TextArea] public string description;

        /// <summary>Converts this effect into a PendingEffect for the resolver stack.</summary>
        public abstract PendingEffect CreateRuntimeEffect(EffectContext context);
    }
}
