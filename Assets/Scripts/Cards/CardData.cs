using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Puckd/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardType type;
    [TextArea] public string description;
    public Sprite artwork;
}