using System.IO;
using System.Security.Cryptography;

namespace ImageSim.Algorithms
{
    public class MD5FileSimilarityAlgorithm : IHashingAlgorithm<MD5HashDescriptor>
    {
        private readonly MD5 algorithm;

        public string Name => "MD5Hash";

        public MD5FileSimilarityAlgorithm()
        {
            algorithm = MD5.Create();
        }

        public MD5HashDescriptor GetDescriptor(string path)
        {
            using var fs = File.OpenRead(path);
            var hash = algorithm.ComputeHash(fs);
            return new MD5HashDescriptor(hash);
        }

        public double GetSimilarity(MD5HashDescriptor left, MD5HashDescriptor right)
        {
            return left.Equals(right) ? 1 : 0;
        }
    }
}
