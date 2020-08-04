namespace ImageSim.Algorithms
{
    public class CachingSimilarityAlgorithm<T> : ISimilarityAlgorithm
    {
        private readonly ICacheService<T> CacheService;
        private readonly IHashingAlgorithm<T> HashingAlgorithm;

        public CachingSimilarityAlgorithm(ICacheService<T> cacheService, IHashingAlgorithm<T> hashingAlgorithm)
        {
            CacheService = cacheService;
            HashingAlgorithm = hashingAlgorithm;
        }

        private T GetOrCreateDescriptor(string path)
        {
            if (CacheService.TryGetValue(path, out T value))
            {
                return value;
            }
            else 
            {
                var descriptor = HashingAlgorithm.GetDescriptor(path);
                CacheService.Add(path, descriptor);
                return descriptor;
            }
        }

        public double GetSimilarity(string left, string right)
        {
            var dLeft = GetOrCreateDescriptor(left);
            var dRight = GetOrCreateDescriptor(right);
            return HashingAlgorithm.GetSimilarity(dLeft, dRight);
        }
    }
}
