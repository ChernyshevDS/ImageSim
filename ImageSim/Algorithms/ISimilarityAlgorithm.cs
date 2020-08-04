using System.Text;
using System.Windows.Navigation;

namespace ImageSim.Algorithms
{
    public interface ISimilarityAlgorithm
    {
        double GetSimilarity(string left, string right);
    }
}
