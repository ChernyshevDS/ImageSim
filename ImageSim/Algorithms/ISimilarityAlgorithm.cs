using ImageSim.Services;
using ImageSim.ViewModels;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    public interface ICacheService<TFeature> : IReadOnlyCollection<KeyValuePair<string, TFeature>>
    {
        bool TryGetValue(string key, out TFeature value);
        void Add(string key, TFeature feature);
        bool Remove(string key);
    }

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

    public class PersistentCacheService<T> : RamCacheService<T> where T : IBinarySerializable, new()
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

    public class AbstractSerializableDescriptor : IBinarySerializable
    {
        public byte[] Data { get; protected set; }

        protected AbstractSerializableDescriptor() { }

        protected AbstractSerializableDescriptor(byte[] data)
        {
            Data = data;
        }

        protected bool DataEquals(in AbstractSerializableDescriptor other)
        {
            if (this.Data == null || other == null || other.Data == null)
                return false;
            return this.Data.AsSpan().SequenceEqual(other.Data);
        }

        public virtual void Deserialize(byte[] data) => Data = data;
        public virtual byte[] Serialize() => Data;
    }

    public class MD5HashDescriptor : AbstractSerializableDescriptor
    {
        public MD5HashDescriptor() { }
        public MD5HashDescriptor(byte[] hash) : base(hash) { }
        public bool DataEquals([AllowNull] MD5HashDescriptor other) => base.DataEquals(other);
    }

    public class MD5HashComparer : IEqualityComparer<MD5HashDescriptor>
    {
        public bool Equals([AllowNull] MD5HashDescriptor x, [AllowNull] MD5HashDescriptor y) => x?.DataEquals(y) ?? false;
        public int GetHashCode([DisallowNull] MD5HashDescriptor obj) => 0;
    }

    public class MD5FileSimilarityAlgorithm : IHashingAlgorithm<MD5HashDescriptor>
    {
        public string Name => "MD5Hash";

        public MD5HashDescriptor GetDescriptor(string path)
        {
            return new MD5HashDescriptor(Utils.GetFileHash(path));
        }

        public double GetSimilarity(MD5HashDescriptor left, MD5HashDescriptor right)
        {
            return left.Equals(right) ? 1 : 0;
        }
    }

    public class DCTImageDescriptor : AbstractSerializableDescriptor
    {
        public DCTImageDescriptor() { }
        public DCTImageDescriptor(byte[] data) : base(data) { }
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
            var hashData = BitConverter.GetBytes(hash);
            return new DCTImageDescriptor(hashData);
        }

        public double GetSimilarity(DCTImageDescriptor left, DCTImageDescriptor right)
        {
            const double max_distance = 64;
            var hashA = BitConverter.ToUInt64(left.Data);
            var hashB = BitConverter.ToUInt64(right.Data);
            var distance = PHash.DCT.HammingDistance(hashA, hashB);
            return 1.0 - (distance / max_distance);
        }
    }

    public class MarrImageDescriptor : AbstractSerializableDescriptor
    {
        public MarrImageDescriptor() { }
        public MarrImageDescriptor(byte[] data) : base(data) { }
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
            var distance = PHash.Marr.HammingDistance(left.Data, right.Data);
            System.Diagnostics.Debug.Assert(distance >= 0 && distance <= 1.0);
            return 1.0 - distance;
        }
    }
}
