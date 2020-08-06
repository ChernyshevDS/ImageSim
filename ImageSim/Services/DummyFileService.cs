using System;
using System.Collections.Generic;

namespace ImageSim.Services
{
    public class DummyFileService : IFileService
    {
        public void DeleteFileToBin(string path) 
        { 
            System.Diagnostics.Debug.WriteLine("DummyFileService.DeleteFileToBin: " + path);
        }

        public IEnumerable<string> EnumerateDirectory(string folder, Predicate<string> filter)
        {
            for (int i = 0; i < 20; i++)
            {
                yield return $"File {i + 1}";
            }
        }
    }
}
