using Actions;
using Effects.Base;

namespace Effects
{
    public struct PendingEffect
    {
        public CardEffect Effect;
        public EffectContext Context;
        public ActionType ActionType;

        public int INTPayload; // optional parameter (for attacks, peek)
    }
}