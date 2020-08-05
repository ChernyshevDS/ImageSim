using ImageSim.Services;

namespace ImageSim.Algorithms
{
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

            //System.Diagnostics.Debug.WriteLine($"{System.IO.Path.GetFileName(path)}: cache updated");
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
}
