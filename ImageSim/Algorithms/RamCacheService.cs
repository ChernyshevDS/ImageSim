using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageSim.Algorithms
{
    public class RamCacheService<T> : ICacheService<T>
        where T : class
    {
        private readonly ConcurrentDictionary<string, T> cache = new ConcurrentDictionary<string, T>();

        public int Count => cache.Count;

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => cache.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public virtual ValueTask<T> TryGetValue(string key)
        {
            if (cache.TryGetValue(key, out var value))
                return ValueTask.FromResult(value);
            else
                return ValueTask.FromResult<T>(null);
        }

        public virtual ValueTask Add(string key, T feature)
        {
            cache.TryAdd(key, feature);
            return ValueTask.CompletedTask;
        }

        public virtual ValueTask<bool> Remove(string key)
        {
            return ValueTask.FromResult(cache.TryRemove(key, out _));
        }
    }
}
