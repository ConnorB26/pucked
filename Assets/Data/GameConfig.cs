using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Puckd/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Start Setup")] [Min(1)] public int startingHandSize = 7;
    [Range(0, 2)] public int startingSaveCards = 1;

    [Header("Timers (seconds)")] [Min(0)] public int mainPhaseSeconds = 45;
    [Min(0)] public int reactionSeconds = 3;

    [Header("Determinism")] public bool useFixedSeed = false;
    public int seed = 12345;

    [Header("Debug")] public bool autoPickFirstLegalTarget = false;
    public bool shuffleBeforeEachGame = true;
}