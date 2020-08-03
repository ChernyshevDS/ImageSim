using ImageSim.Services;
using ImageSim.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Navigation;

namespace ImageSim.Algorithms
{
    public interface ISimilarityAlgorithm
    {
        double GetSimilarity(string left, string right);
    }

    public interface IHashingAlgorithm<TFeature>
    {
        string Name { get; }
        TFeature GetDescriptor(string path);
        double GetSimilarity(TFeature left, TFeature right);
    }

    //class Foo
    //{
    //    void Bar()
    //    {
    //        var alg = new DCTImageSimilarityAlgorithm();
    //        alg.Options.ClampSize = new Size(512, 512);

    //        var storage = new PersistentStorage(BlobCache.LocalMachine);

    //        var sim = alg.WithPersistentCache(storage);
            
            
    //    }
    //}

    public interface ICacheService<TFeature> : IReadOnlyCollection<KeyValuePair<string, TFeature>>
    {
        bool TryGetValue(string key, out TFeature value);
        void Add(string key, TFeature feature);
        bool Remove(string key);
    }

    public class RamCacheService<T> : ICacheService<T>
    {
        private readonly Dictionary<string, T> cache = new Dictionary<string, T>();

        public int Count => cache.Count;

        public virtual void Add(string key, T feature) => cache.Add(key, feature);
        public virtual bool Remove(string key) => cache.Remove(key);
        public virtual bool TryGetValue(string key, out T value) => cache.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => cache.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class SimilarityAlgorithms
    {
        public static ISimilarityAlgorithm WithCache<T>(this IHashingAlgorithm<T> alg)
        {
            var ramCache = new RamCacheService<T>();
            return new CachingSimilarityAlgorithm<T>(ramCache, alg);
        }

        public static ISimilarityAlgorithm WithPersistentCache<T>(this IHashingAlgorithm<T> alg, IFileDataStorage storage)
        {
            var cache = new PersistentCacheService<T>(storage, alg.Name);
            return new CachingSimilarityAlgorithm<T>(cache, alg);
        }
    }

    public class PersistentCacheService<T> : RamCacheService<T>
    {
        private readonly IFileDataStorage storage;
        private readonly string dataKey;

        public PersistentCacheService(IFileDataStorage storage, string dataKey)
        {
            this.storage = storage;
            this.dataKey = dataKey;
        }
        
        public override void Add(string path, T feature)
        {
            var cachedRecord = storage.GetFileRecordAsync(path).Result;
            if (cachedRecord == null)   //no cache record found
                cachedRecord = PersistentFileRecord.Create(path);

            cachedRecord.SetData(dataKey, feature);
            storage.UpdateFileRecordAsync(path, cachedRecord).Wait();

            System.Diagnostics.Debug.WriteLine($"{System.IO.Path.GetFileName(path)}: cache updated");
            base.Add(path, feature);
        }

        public override bool TryGetValue(string key, out T value)
        {
            if (base.TryGetValue(key, out value))   //cache is already loaded
                return true;

            var cachedRecord = storage.GetFileRecordAsync(key).Result;
            if (cachedRecord == null)   //no cache record found
                return false;

            var time = PersistentFileRecord.ReadModificationTime(key);
            if (!time.HasValue) //current file can't be read - skip
                return false;

            if (cachedRecord.Modified != time)  //cached value expired
            {
                storage.RemoveFileRecordAsync(key).Wait();
                return false;
            }

            if (!cachedRecord.TryGetData<T>(dataKey, out T data))   //no cached Hash
                return false;

            //success - use cached value
            base.Add(key, data);
            value = data;
            return true;
        }
    }

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

    public class MD5FileSimilarityAlgorithm : IHashingAlgorithm<string>
    {
        public string Name => "MD5Hash";

        public string GetDescriptor(string path)
        {
            return Utils.GetFileHash(path);
        }

        public double GetSimilarity(string left, string right)
        {
            return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase) ? 1 : 0;
        }
    }

    public class DCTImageDescriptor
    {
        public DCTImageDescriptor(ulong data)
        {
            Data = data;
        }

        public ulong Data { get; }
    }

    public class DCTImageSimilarityAlgorithm : IHashingAlgorithm<DCTImageDescriptor>
    {
        public string Name => "DCTImageHash";

        public class AlgorithmOptions
        {
            public Size ClampSize { get; set; } = Size.Empty;
        }

        public AlgorithmOptions Options { get; } = new AlgorithmOptions();

        public DCTImageDescriptor GetDescriptor(string path)
        {
            var hash = PHash.DCT.GetImageHash(path, (int)Options.ClampSize.Width, (int)Options.ClampSize.Height);
            return new DCTImageDescriptor(hash);
        }

        public double GetSimilarity(DCTImageDescriptor left, DCTImageDescriptor right)
        {
            return PHash.DCT.HammingDistance(left.Data, right.Data);
        }
    }

    public class MarrImageDescriptor
    {
        public MarrImageDescriptor(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; }
    }

    public class MarrImageSimilarityAlgorithm : IHashingAlgorithm<MarrImageDescriptor>
    {
        public string Name => "MarrImageHash";

        public AlgorithmOptions Options { get; } = new AlgorithmOptions();

        public class AlgorithmOptions
        {
            public float Alpha { get; set; } = 2f;
            public float Level { get; set; } = 1;
        }

        public MarrImageDescriptor GetDescriptor(string path)
        {
            var descr = PHash.Marr.GetImageHash(path, Options.Alpha, Options.Level);
            return new MarrImageDescriptor(descr);
        }

        public double GetSimilarity(MarrImageDescriptor left, MarrImageDescriptor right)
        {
            var sim = PHash.Marr.HammingDistance(left.Data, right.Data);
            return sim;
        }
    }
}
