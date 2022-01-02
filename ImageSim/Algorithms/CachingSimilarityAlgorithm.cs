using System.Threading.Tasks;

namespace ImageSim.Algorithms
{
    public class CachingSimilarityAlgorithm<T> : ISimilarityAlgorithm
        where T: class
    {
        private readonly ICacheService<T> CacheService;
        private readonly IHashingAlgorithm<T> HashingAlgorithm;

        public CachingSimilarityAlgorithm(ICacheService<T> cacheService, IHashingAlgorithm<T> hashingAlgorithm)
        {
            CacheService = cacheService;
            HashingAlgorithm = hashingAlgorithm;
        }

        private async ValueTask<T> GetOrCreateDescriptor(string path)
        {
            var value = await CacheService.TryGetValue(path);
            if (value != null)
            {
                return value;
            }
            else 
            {
                var descriptor = HashingAlgorithm.GetDescriptor(path);
                await CacheService.Add(path, descriptor);
                return descriptor;
            }
        }

        public async ValueTask<double> GetSimilarity(string left, string right)
        {
            var dLeft = await GetOrCreateDescriptor(left);
            var dRight = await GetOrCreateDescriptor (right);
            return HashingAlgorithm.GetSimilarity(dLeft, dRight);
        }
    }
}
