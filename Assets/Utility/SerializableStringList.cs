using System;
using Unity.Collections;
using Unity.Netcode;

namespace Utility
{
    [Serializable]
    public struct SerializableStringList : INetworkSerializable
    {
        public FixedString128Bytes[] values;

        public SerializableStringList(string[] source)
        {
            if (source == null)
            {
                values = Array.Empty<FixedString128Bytes>();
                return;
            }

            values = new FixedString128Bytes[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                values[i] = new FixedString128Bytes(source[i] ?? "");
            }
        }

        public string[] ToStringArray()
        {
            var result = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
                result[i] = values[i].ToString();
            return result;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            var len = values?.Length ?? 0;
            serializer.SerializeValue(ref len);

            if (serializer.IsReader)
            {
                values = new FixedString128Bytes[len];
            }

            for (var i = 0; i < len; i++)
                serializer.SerializeValue(ref values[i]);
        }
    }
}