using Effects.Base;

namespace Actions
{
    /// <summary>An action produced by EffectResolver and consumed by GameActionExecutor.</summary>
    public struct GameAction
    {
        public readonly ActionType Type;
        public readonly int Value;
        public EffectContext Context;

        public GameAction(ActionType type, int value, EffectContext context)
        {
            Type = type;
            Value = value;
            Context = context;
        }
    }
}
