public sealed class GameConfigDTO
{
    public int StartingHandSize = 7;
    public int StartingSaveCards = 1;
    public bool UseFixedSeed = false;
    public int Seed = 12345;
    public int MainPhaseSeconds = 45; // timers are data here; enforcement optional in core
    public int ReactionSeconds = 3;
}