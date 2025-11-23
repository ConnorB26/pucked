public sealed class CardInstance
{
    public readonly int InstanceId; // unique per copy
    public readonly string DefId; // back to CardDef

    public CardInstance(int instanceId, string defId)
    {
        InstanceId = instanceId;
        DefId = defId;
    }
}