using System.Text;

namespace ImageSim.Services.Storage
{
    public interface IFileRecordData
    {
        string Key { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }
}
