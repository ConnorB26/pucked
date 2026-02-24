namespace Cards
{
    /// <summary>A specific runtime card with a unique instance ID and its definition.</summary>
    public struct CardInstance
    {
        public int InstanceId; // unique per match
        public CardDefinition Definition;

        public CardInstance(int instanceId, CardDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition;
        }
    }
}
