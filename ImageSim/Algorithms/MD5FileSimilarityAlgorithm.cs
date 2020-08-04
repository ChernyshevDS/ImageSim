using ImageSim.ViewModels;

namespace ImageSim.Algorithms
{
    public class MD5FileSimilarityAlgorithm : IHashingAlgorithm<MD5HashDescriptor>
    {
        public string Name => "MD5Hash";

        public MD5HashDescriptor GetDescriptor(string path)
        {
            return new MD5HashDescriptor(Utils.GetFileHash(path));
        }

        public double GetSimilarity(MD5HashDescriptor left, MD5HashDescriptor right)
        {
            return left.Equals(right) ? 1 : 0;
        }
    }
}
