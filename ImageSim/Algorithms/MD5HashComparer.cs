using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ImageSim.Algorithms
{
    public class MD5HashComparer : IEqualityComparer<MD5HashDescriptor>
    {
        public bool Equals([AllowNull] MD5HashDescriptor x, [AllowNull] MD5HashDescriptor y) => x?.DataEquals(y) ?? false;
        public int GetHashCode([DisallowNull] MD5HashDescriptor obj) => 0;
    }
}
