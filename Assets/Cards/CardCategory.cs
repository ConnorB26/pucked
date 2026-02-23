namespace Cards
{
    /// <summary>Categorizes a card's behavior for game logic and UI display.</summary>
    public enum CardCategory
    {
        Puckd,       // Elimination card — drawn from deck, never played from hand
        GoalieSave,  // Auto-consumed when a Puck'd is drawn to prevent elimination
        Cancel,
        Attack,
        Skip,
        Peek,
        Shuffle
    }
}
