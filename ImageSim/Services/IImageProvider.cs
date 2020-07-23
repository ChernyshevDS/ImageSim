using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Threading;

namespace ImageSim.Services
{
    public interface IImageProvider
    {
        string WorkingFolder { get; set; }
        IAsyncEnumerable<string> GetFilesAsync(Predicate<string> filter, CancellationToken token = default);
    }
}
