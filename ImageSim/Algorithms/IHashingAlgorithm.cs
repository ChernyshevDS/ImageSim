namespace ImageSim.Algorithms
{
    public interface IHashingAlgorithm<TFeature>
    {
        string Name { get; }
        TFeature GetDescriptor(string path);
        double GetSimilarity(TFeature left, TFeature right);
    }
}
