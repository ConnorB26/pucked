using System;
using Unity.Netcode;
using Utility;

namespace Networking.Snapshots
{
    /// <summary>Network-serializable peek result, sent privately to the peeking player's client.</summary>
    [Serializable]
    public struct PeekSnapshot : INetworkSerializable
    {
        public int playerId;
        public SerializableStringList names;
        public int[] categories;

        public PeekSnapshot(int playerId, string[] names, int[] categories)
        {
            this.playerId = playerId;
            this.names = new SerializableStringList(names);
            this.categories = categories;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerId);

            names.NetworkSerialize(serializer);

            var len = categories?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader) categories = new int[len];
            for (var i = 0; i < len; i++)
                serializer.SerializeValue(ref categories[i]);
        }
    }
}