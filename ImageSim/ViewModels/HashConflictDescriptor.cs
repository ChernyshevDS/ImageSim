namespace ImageSim.ViewModels
{
    public readonly struct HashConflictDescriptor
    {
        internal readonly string[] Paths;
        internal readonly byte[] Hash;

        internal HashConflictDescriptor(string[] paths, byte[] hash)
        {
            Paths = paths;
            Hash = hash;
        }
    }
}
