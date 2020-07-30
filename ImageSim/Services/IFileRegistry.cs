using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Text;
using System.Threading;

namespace ImageSim.Services
{
    public interface IFileRegistry
    {
        IAsyncEnumerable<string> GetFilesAsync(string folder, Predicate<string> filter, CancellationToken token = default);
        void DeleteFileToBin(string path);
    }
}
