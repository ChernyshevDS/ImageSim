using System;
using System.Collections.Generic;

namespace ImageSim.Services.Storage
{
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

        public void SetData<T>(T data) where T : IFileRecordData
        {
            if (Data == null)
                Data = new Dictionary<string, byte[]>();
            Data[data.Key] = data.Serialize();
        }

        public bool TryGetData<T>(string key, out T value) where T : IFileRecordData, new()
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
}
