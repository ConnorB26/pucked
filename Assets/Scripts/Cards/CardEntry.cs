using UnityEngine;

[System.Serializable]
public class CardEntry
{
    public CardData cardData;
    [Min(1)] public int count = 1;
}