using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Akavache;
using System.Threading.Tasks;
using System;

namespace ImageSim.Services
{
    public class PersistentStorage : IFileDataStorage
    {
        private readonly IBlobCache Cache;
        
        public PersistentStorage(IBlobCache cache)
        {
            Cache = cache;
            Cache.ForcedDateTimeKind = System.DateTimeKind.Utc;
        }

        public async Task<PersistentFileRecord> GetFileRecordAsync(string path)
        {
            return await Cache.GetObject<PersistentFileRecord>(path)
                .Catch(Observable.Return<PersistentFileRecord>(null));
        }

        public async Task UpdateFileRecordAsync(string path, PersistentFileRecord record)
        {
            await Cache.InsertObject(path, record);
        }

        public async Task RemoveFileRecordAsync(string path)
        {
            await Cache.Invalidate(path);
        }

        public IEnumerable<string> GetAllKeys()
        {
            return Cache.GetAllObjects<PersistentFileRecord>().Wait().Select(x => x.FilePath);
        }

        public async Task Invalidate()
        {
            await Cache.InvalidateAll();
        }
    }

    public interface IBinarySerializable
    {
        byte[] Serialize();
        void Deserialize(byte[] data);
    }

    public class PersistentFileRecord
    {
        public string FilePath { get; set; }
        public DateTime? Modified { get; set; }
        public Dictionary<string, byte[]> Data { get; set; }

        public PersistentFileRecord()
        {
        }

        public static PersistentFileRecord Create(string path)
        {
            return new PersistentFileRecord()
            {
                FilePath = path,
                Modified = ReadModificationTime(path)
            };
        }

        public static DateTime? ReadModificationTime(string path)
        {
            try
            {
                return System.IO.File.GetLastWriteTimeUtc(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void RemoveData(string key)
        {
            Data?.Remove(key);
        }

        public void SetData<T>(string key, T data) where T : IBinarySerializable
        {
            if (Data == null)
                Data = new Dictionary<string, byte[]>();
            Data[key] = data.Serialize();
        }

        public bool TryGetData<T>(string key, out T value) where T : IBinarySerializable, new()
        {
            if (Data != null && Data.TryGetValue(key, out byte[] data))
            {
                value = new T();
                value.Deserialize(data);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }
    }

    public class DummyStorage : IFileDataStorage
    {
        public IEnumerable<string> GetAllKeys() => Enumerable.Empty<string>();
        public Task<PersistentFileRecord> GetFileRecordAsync(string path) => Task.FromResult<PersistentFileRecord>(null);
        public Task Invalidate() => Task.CompletedTask;
        public Task RemoveFileRecordAsync(string path) => Task.CompletedTask;
        public Task UpdateFileRecordAsync(string path, PersistentFileRecord record) => Task.CompletedTask;
    }
}
