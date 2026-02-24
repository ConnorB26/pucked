using System;
using Unity.Netcode;
using Utility;

namespace Networking.Snapshots
{
    /// <summary>Network-serializable snapshot of a player's hand, sent per-player after each state change.</summary>
    [Serializable]
    public struct HandSnapshot : INetworkSerializable
    {
        public int playerId;
        public int[] instanceIds;
        public SerializableStringList names;
        public int[] categories;

        public HandSnapshot(int playerId, int[] instanceIds, string[] names, int[] categories)
        {
            this.playerId = playerId;
            this.instanceIds = instanceIds;
            this.names = new SerializableStringList(names);
            this.categories = categories;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerId);

            var len = instanceIds?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader) instanceIds = new int[len];
            for (var i = 0; i < len; i++)
                serializer.SerializeValue(ref instanceIds[i]);

            names.NetworkSerialize(serializer);

            var clen = categories?.Length ?? 0;
            serializer.SerializeValue(ref clen);
            if (serializer.IsReader) categories = new int[clen];
            for (var i = 0; i < clen; i++)
                serializer.SerializeValue(ref categories[i]);
        }
    }
}