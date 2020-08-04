using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using ImageSim.Messages;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.VisualBasic.FileIO;

namespace ImageSim.Services
{
    public class FileService : IFileService
    {
        public void DeleteFileToBin(string path)
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        public IEnumerable<string> EnumerateDirectory(string folder, Predicate<string> filter)
        {
            if (!Directory.Exists(folder))
                return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(folder, "*", System.IO.SearchOption.AllDirectories).Where(x => filter(x));
        }
    }
}
