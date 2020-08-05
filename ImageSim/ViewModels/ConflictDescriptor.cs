namespace ImageSim.ViewModels
{
    public readonly struct ConflictDescriptor
    {
        internal readonly string Left;
        internal readonly string Right;
        internal readonly double Similarity;

        internal ConflictDescriptor(string left, string right, double metric)
        {
            Left = left;
            Right = right;
            Similarity = metric;
        }
    }
}
