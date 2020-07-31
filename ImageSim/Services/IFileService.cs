using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Text;
using System.Threading;

namespace ImageSim.Services
{
    public interface IFileService
    {
        IEnumerable<string> EnumerateDirectory(string folder, Predicate<string> filter);
        void DeleteFileToBin(string path);
    }
}
