namespace Effects.Base
{
    /// <summary>Per-effect runtime context passed through the resolver to GameActionExecutor.</summary>
    public struct EffectContext
    {
        public int OwnerPlayerId;
        public int TargetPlayerId; // -1 or 0 if untargeted
        public int CardId;
    }
}
