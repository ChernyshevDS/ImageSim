using System.Collections.Generic;
using System.Threading.Tasks;
using ImageSim.Services.Storage;

namespace ImageSim.Services
{
    public interface IFileDataStorage
    {
        Task<PersistentFileRecord> GetFileRecordAsync(string path);
        Task UpdateFileRecordAsync(string path, PersistentFileRecord record);
        Task RemoveFileRecordAsync(string path);
        IEnumerable<string> GetAllKeys();
        Task Invalidate();
    }
}
