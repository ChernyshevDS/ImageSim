using System;
using System.Windows;

namespace ImageSim.Algorithms
{
    public class DCTImageSimilarityAlgorithm : IHashingAlgorithm<DCTImageDescriptor>
    {
        public string Name => "DCTImageHash";

        public class AlgorithmOptions
        {
            public Size ClampSize { get; set; } = Size.Empty;
        }

        public AlgorithmOptions Options { get; } = new AlgorithmOptions();

        public DCTImageDescriptor GetDescriptor(string path)
        {
            var hash = PHash.DCT.GetImageHash(path, (int)Options.ClampSize.Width, (int)Options.ClampSize.Height);
            var hashData = BitConverter.GetBytes(hash);
            return new DCTImageDescriptor(hashData);
        }

        public double GetSimilarity(DCTImageDescriptor left, DCTImageDescriptor right)
        {
            const double max_distance = 64;
            var hashA = BitConverter.ToUInt64(left.Data);
            var hashB = BitConverter.ToUInt64(right.Data);
            var distance = PHash.DCT.HammingDistance(hashA, hashB);
            return 1.0 - (distance / max_distance);
        }
    }
}
