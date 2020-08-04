using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImageSim.Services
{
    public class DummyStorage : IFileDataStorage
    {
        public IEnumerable<string> GetAllKeys() => Enumerable.Empty<string>();
        public Task<PersistentFileRecord> GetFileRecordAsync(string path) => Task.FromResult<PersistentFileRecord>(null);
        public Task Invalidate() => Task.CompletedTask;
        public Task RemoveFileRecordAsync(string path) => Task.CompletedTask;
        public Task UpdateFileRecordAsync(string path, PersistentFileRecord record) => Task.CompletedTask;
    }
}
