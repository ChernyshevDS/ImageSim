using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Threading;

namespace ImageSim.Services
{
    public interface IImageProvider
    {
        IAsyncEnumerable<string> GetFilesAsync(string folder, Predicate<string> filter, CancellationToken token = default);
    }
}
