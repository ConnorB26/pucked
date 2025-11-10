public enum CardType
{
    Puckd, // Core danger card that eliminates players
    Save, // Goalie Save cards that defend against Puckd
    Cancel, // Cards that can interrupt and block other player's actions (Offside!, Coach's Challenge)
    Attack, // Forces next player to take extra turns (Body Check, Power Play)
    Skip, // End turn without drawing (Line Change)
    Peek, // View top cards of deck (Scout the Ice, Instant Replay)
    Shuffle // Reshuffles the deck (Zamboni Pass)
}