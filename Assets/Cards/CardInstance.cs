namespace Cards
{
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