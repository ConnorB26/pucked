using Actions;
using Effects.Base;

namespace Effects
{
    /// <summary>An effect queued on the EffectResolver stack, awaiting resolution into a GameAction.</summary>
    public struct PendingEffect
    {
        public CardEffect Effect;
        public EffectContext Context;
        public ActionType ActionType;
        public int INTPayload; // optional int parameter (e.g. extraTurns for Attack, peekAmount for Peek)
    }
}
