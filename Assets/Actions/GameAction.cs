using Effects.Base;

namespace Actions
{
    public struct GameAction
    {
        public ActionType Type;
        public int Value;
        public EffectContext Context;

        public GameAction(ActionType type, int value, EffectContext context)
        {
            Type = type;
            Value = value;
            Context = context;
        }
    }
}