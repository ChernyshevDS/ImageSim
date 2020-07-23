namespace ImageSim.Services.Storage
{
    public class HashData : IFileRecordData
    {
        public static string Key => "Hash";
        public string Hash { get; set; }

        string IFileRecordData.Key => HashData.Key;

        public void Deserialize(byte[] data)
        {
            Hash = System.Text.Encoding.ASCII.GetString(data);
        }

        public byte[] Serialize()
        {
            return System.Text.Encoding.ASCII.GetBytes(Hash);
        }
    }
}
