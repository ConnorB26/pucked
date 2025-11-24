using System;
using Unity.Netcode;
using Utility;

namespace Networking.Snapshots
{
    [Serializable]
    public struct LobbyStateSnapshot : INetworkSerializable
    {
        public int phase;
        public int maxPlayers;
        public ulong[] clientIds;
        public SerializableStringList names;
        public SerializableStringList colors;
        public bool[] readyFlags;

        public LobbyStateSnapshot(
            int phase,
            ulong[] clientIds,
            string[] names,
            string[] colors,
            bool[] readyFlags,
            int maxPlayers)
        {
            this.phase = phase;
            this.maxPlayers = maxPlayers;
            this.clientIds = clientIds;
            this.names = new SerializableStringList(names);
            this.colors = new SerializableStringList(colors);
            this.readyFlags = readyFlags;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref phase);
            serializer.SerializeValue(ref maxPlayers);

            // clientIds
            var len = clientIds?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader) clientIds = new ulong[len];
            for (var i = 0; i < len; i++)
                serializer.SerializeValue(ref clientIds[i]);

            // names + colors use their own custom network serialize
            names.NetworkSerialize(serializer);
            colors.NetworkSerialize(serializer);

            // readyFlags
            var rlen = readyFlags?.Length ?? 0;
            serializer.SerializeValue(ref rlen);
            if (serializer.IsReader) readyFlags = new bool[rlen];
            for (var i = 0; i < rlen; i++)
                serializer.SerializeValue(ref readyFlags[i]);
        }
    }
}