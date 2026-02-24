namespace Actions
{
    /// <summary>Discriminates the action produced by an effect after EffectResolver processing.</summary>
    public enum ActionType
    {
        RequestElimination,
        PreventElimination,
        CancelLastEffect,
        ForceExtraTurns,
        SkipTurn,
        PeekCards,
        ShuffleDeck
    }
}
