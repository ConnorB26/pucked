using UnityEngine;

public enum TargetMode
{
    None,
    Self,
    SingleOpponent,
    AnyPlayer,
    AllOpponents
}

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

[CreateAssetMenu(fileName = "CardConfig", menuName = "Puckd/Card")]
public class CardConfig : ScriptableObject
{
    [Header("Identity")] [Tooltip("Stable unique ID used by the core engine (auto-filled from asset GUID).")]
    public string defId; // filled by editor; kept in asset for runtime

    public string cardName = "New Card";
    public CardType type;
    [TextArea] public string description;
    public Sprite artwork;

    [Header("Play & Counter")] public TargetMode targetMode = TargetMode.None;

    [Tooltip("If true, opponents may Cancel this card in a reaction window.")]
    public bool canBeCountered = true;

    [Header("Type Parameters (shown by editor)")]
    public AttackParams attack; // used when type == Attack

    public PeekParams peek; // used when type == Peek

    [System.Serializable]
    public struct AttackParams
    {
        public int extraTurns;
    }

    [System.Serializable]
    public struct PeekParams
    {
        public int count;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep defId synced to the asset GUID if missing.
        if (string.IsNullOrEmpty(defId))
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                defId = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        // Sensible defaults
        if (type == CardType.Attack && attack.extraTurns <= 0) attack.extraTurns = 2;
        if (type == CardType.Peek && peek.count <= 0) peek.count = 3;
        if (type == CardType.Puckd) canBeCountered = false; // cannot counter Puck’d draw
        if (type == CardType.Save) canBeCountered = false; // saving shouldn't be countered
    }
#endif
}