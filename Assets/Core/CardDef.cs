public sealed class CardDef
{
    public readonly string DefId; // unique stable id (e.g., GUID or CardData asset name)
    public readonly string Name;
    public readonly CardType Type;

    public CardDef(string defId, string name, CardType type)
    {
        DefId = defId;
        Name = name;
        Type = type;
    }
}