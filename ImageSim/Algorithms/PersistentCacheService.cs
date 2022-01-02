using ImageSim.Services;
using System.Threading.Tasks;

namespace ImageSim.Algorithms
{
    public class PersistentCacheService<T> : RamCacheService<T> where T : class, IBinarySerializable, new()
    {
        private readonly IFileDataStorage storage;
        private readonly string dataKey;

        public PersistentCacheService(IFileDataStorage storage, string dataKey)
        {
            this.storage = storage;
            this.dataKey = dataKey;
        }
        
        public override async ValueTask Add(string path, T feature)
        {
            var cachedRecord = await storage.GetFileRecordAsync(path);
            if (cachedRecord == null)   //no cache record found
                cachedRecord = PersistentFileRecord.Create(path);

            cachedRecord.SetData(dataKey, feature);
            await storage.UpdateFileRecordAsync(path, cachedRecord);

            //System.Diagnostics.Debug.WriteLine($"{System.IO.Path.GetFileName(path)}: cache updated");
            await base.Add(path, feature);
        }

        public override async ValueTask<T> TryGetValue(string key)
        {
            var value = await base.TryGetValue(key);
            if (value != null)   //cache is already loaded
                return value;

            var cachedRecord = await storage.GetFileRecordAsync(key);
            if (cachedRecord == null)   //no cache record found
                return null;

            var time = PersistentFileRecord.ReadModificationTime(key);
            if (!time.HasValue) //current file can't be read - skip
                return null;

            if (cachedRecord.Modified != time)  //cached value expired
            {
                await storage.RemoveFileRecordAsync(key);
                return null;
            }

            if (!cachedRecord.TryGetData<T>(dataKey, out T data))   //no cached Hash
                return null;

            //success - use cached value
            await base.Add(key, data);
            value = data;
            return value;
        }
    }
}
