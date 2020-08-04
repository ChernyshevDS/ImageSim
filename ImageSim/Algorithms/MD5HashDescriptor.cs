using System.Diagnostics.CodeAnalysis;

namespace ImageSim.Algorithms
{
    public class MD5HashDescriptor : AbstractSerializableDescriptor
    {
        public MD5HashDescriptor() { }
        public MD5HashDescriptor(byte[] hash) : base(hash) { }
        public bool DataEquals([AllowNull] MD5HashDescriptor other) => base.DataEquals(other);
    }
}
