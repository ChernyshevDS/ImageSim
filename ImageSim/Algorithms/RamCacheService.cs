using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ImageSim.Algorithms
{
    public class RamCacheService<T> : ICacheService<T>
    {
        private readonly ConcurrentDictionary<string, T> cache = new ConcurrentDictionary<string, T>();

        public int Count => cache.Count;

        public virtual void Add(string key, T feature) => cache.TryAdd(key, feature);
        public virtual bool Remove(string key) => cache.TryRemove(key, out _);
        public virtual bool TryGetValue(string key, out T value) => cache.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => cache.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
