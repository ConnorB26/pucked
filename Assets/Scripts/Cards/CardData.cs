using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines a card's properties and behavior.
/// Create new cards through the Unity menu: Assets > Create > Puckd > Card
/// </summary>
[CreateAssetMenu(fileName = "NewCard", menuName = "Puckd/Card")]
public class CardData : ScriptableObject
{
    [Header("Basic Information")] [Tooltip("The name of the card as shown to players")]
    public string cardName;

    [Tooltip("The type of card, which determines its basic behavior")]
    public CardType type;

    [TextArea(3, 5)] [Tooltip("Card description shown to players")]
    public string description;

    [Tooltip("Card artwork shown in the UI")]
    public Sprite artwork;

    [Header("Core Properties")] [Tooltip("Whether this card can be countered by Cancel cards")]
    public bool canBeCountered = true;

    [Tooltip("Whether this card requires a target player")] [SerializeField]
    private bool requiresTarget;

    public bool RequiresTarget => requiresTarget; // Read-only public access

    [Header("Type-Specific Properties")]
    [Tooltip("For Peek cards: Number of cards player can look at (1-5)")]
    [Range(1, 5)]
    public int peekAmount = 3;

    [Tooltip("For Attack cards: Number of extra turns the target must take (1-3)")] [Range(1, 3)]
    public int extraTurns = 2;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure card has a name
        if (string.IsNullOrEmpty(cardName))
        {
            cardName = name;
        }

        // Set default targeting based on type
        requiresTarget = type == CardType.Attack;

        // Ensure values are within reasonable ranges
        switch (type)
        {
            case CardType.Peek:
                peekAmount = Mathf.Clamp(peekAmount, 1, 5);
                break;
            case CardType.Attack:
                extraTurns = Mathf.Clamp(extraTurns, 1, 3);
                break;
        }

        // Update asset name to match card name
        if (!string.IsNullOrEmpty(cardName) && name != cardName)
        {
            AssetDatabase.RenameAsset(
                AssetDatabase.GetAssetPath(this),
                cardName.Replace(" ", "_"));
        }
    }
#endif
}