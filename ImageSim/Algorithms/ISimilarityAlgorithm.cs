using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace ImageSim.Algorithms
{
    public interface ISimilarityAlgorithm
    {
        ValueTask<double> GetSimilarity(string left, string right);
    }
}
