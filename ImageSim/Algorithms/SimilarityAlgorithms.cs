using ImageSim.Services;

namespace ImageSim.Algorithms
{
    public static class SimilarityAlgorithms
    {
        public static ISimilarityAlgorithm WithCache<T>(this IHashingAlgorithm<T> alg)
        {
            var ramCache = new RamCacheService<T>();
            return new CachingSimilarityAlgorithm<T>(ramCache, alg);
        }

        public static ISimilarityAlgorithm WithPersistentCache<T>(this IHashingAlgorithm<T> alg, IFileDataStorage storage)
            where T : IBinarySerializable, new()
        {
            var cache = new PersistentCacheService<T>(storage, alg.Name);
            return new CachingSimilarityAlgorithm<T>(cache, alg);
        }
    }
}
