using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageSim.Algorithms
{
    public interface ICacheService<TFeature> : IReadOnlyCollection<KeyValuePair<string, TFeature>>
        where TFeature: class
    {
        ValueTask<TFeature> TryGetValue(string key);
        ValueTask Add(string key, TFeature feature);
        ValueTask<bool> Remove(string key);
    }
}
