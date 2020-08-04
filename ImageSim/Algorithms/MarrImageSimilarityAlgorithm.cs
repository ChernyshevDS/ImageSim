namespace ImageSim.Algorithms
{
    public class MarrImageSimilarityAlgorithm : IHashingAlgorithm<MarrImageDescriptor>
    {
        public string Name => "MarrImageHash";

        public AlgorithmOptions Options { get; } = new AlgorithmOptions();

        public class AlgorithmOptions
        {
            public float Alpha { get; set; } = 2f;
            public float Level { get; set; } = 1;
        }

        public MarrImageDescriptor GetDescriptor(string path)
        {
            var descr = PHash.Marr.GetImageHash(path, Options.Alpha, Options.Level);
            return new MarrImageDescriptor(descr);
        }

        public double GetSimilarity(MarrImageDescriptor left, MarrImageDescriptor right)
        {
            var distance = PHash.Marr.HammingDistance(left.Data, right.Data);
            System.Diagnostics.Debug.Assert(distance >= 0 && distance <= 1.0);
            return 1.0 - distance;
        }
    }
}
