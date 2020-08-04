using System.Collections.Generic;

namespace ImageSim.Algorithms
{
    public interface ICacheService<TFeature> : IReadOnlyCollection<KeyValuePair<string, TFeature>>
    {
        bool TryGetValue(string key, out TFeature value);
        void Add(string key, TFeature feature);
        bool Remove(string key);
    }
}
