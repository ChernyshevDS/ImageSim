using System;

namespace ImageSim.Services.Storage
{
    public class DCTImageHashData : IFileRecordData
    {
        public static string Key => "DCTImageHash";
        string IFileRecordData.Key => DCTImageHashData.Key;

        public ulong Hash { get; set; }

        public void Deserialize(byte[] data)
        {
            Hash = BitConverter.ToUInt64(data);
        }

        public byte[] Serialize()
        {
            return BitConverter.GetBytes(Hash);
        }
    }
}
