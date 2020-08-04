using GalaSoft.MvvmLight;
using System;
using System.IO;

namespace ImageSim.ViewModels
{
    public class FileDetailsVM : ViewModelBase
    {
        private string filePath;
        private long fileSize;

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public long FileSize { get => fileSize; set => Set(ref fileSize, value); }

        public FileDetailsVM(string path)
        {
            FilePath = path;
            try
            {
                FileInfo fi = new FileInfo(FilePath);
                FileSize = fi.Length;
            }
            catch (Exception)
            {
                FileSize = 0;
            }            
        }
    }
}
