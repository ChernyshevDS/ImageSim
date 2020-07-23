using System.Collections.Generic;
using System.Linq;

using System.Reactive.Linq;
using Akavache;
using System.Threading.Tasks;
using ImageSim.Services.Storage;

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
}
