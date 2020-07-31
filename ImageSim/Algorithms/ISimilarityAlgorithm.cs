using ImageSim.Services;
using ImageSim.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

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

    class Foo
    {
        void Bar()
        {
            var alg = new DCTImageSimilarityAlgorithm();
            alg.Options.ClampSize = new Size(512, 512);

            var cacheAlg = new CachingSimilarityAlgorithm<DCTImageDescriptor>(null, alg);
            
        }
    }

    public interface ICacheService<TFeature>
    {
        bool TryGetValue(string key, out TFeature value);
        void Add(string key, TFeature feature);
    }

    public class RamCacheService<T> : ICacheService<T>
    {
        private readonly Dictionary<string, T> cache = new Dictionary<string, T>();
        public virtual void Add(string key, T feature) => cache.Add(key, feature);
        public virtual bool TryGetValue(string key, out T value) => cache.TryGetValue(key, out value);
    }

    public class PersistentCacheService<T> : RamCacheService<T>
    {
        private readonly IFileDataStorage storage;

        public PersistentCacheService(Services.IFileDataStorage storage)
        {
            this.storage = storage;
        }
        
        public override void Add(string key, T feature)
        {
            var record = storage.GetFileRecordAsync(key);
            
            base.Add(key, feature);
        }

        public override bool TryGetValue(string key, out T value)
        {
            return base.TryGetValue(key, out value);
        }
    }

    /*public class PersistentCacheService : ICacheService
    {
        public void Add<TFeature>(string path, string key, TFeature feature)
        {
            
        }

        public bool TryGetValue<TFeature>(string path, string key, out TFeature value)
        {
            
        }
    }*/

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
