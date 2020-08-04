using ImageSim.Services;
using System;

namespace ImageSim.Algorithms
{
    public class AbstractSerializableDescriptor : IBinarySerializable
    {
        public byte[] Data { get; protected set; }

        protected AbstractSerializableDescriptor() { }

        protected AbstractSerializableDescriptor(byte[] data)
        {
            Data = data;
        }

        protected bool DataEquals(in AbstractSerializableDescriptor other)
        {
            if (this.Data == null || other == null || other.Data == null)
                return false;
            return this.Data.AsSpan().SequenceEqual(other.Data);
        }

        public virtual void Deserialize(byte[] data) => Data = data;
        public virtual byte[] Serialize() => Data;
    }
}
