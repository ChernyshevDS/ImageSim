using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ImageSim.Services
{
    public class FSImageProvider : IImageProvider
    {
        public string WorkingFolder { get; set; } = string.Empty;

        public async IAsyncEnumerable<string> GetFilesAsync(Predicate<string> filter, 
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (!Directory.Exists(WorkingFolder))
                yield break;

            var localOpts = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.PlatformDefault,
                MatchType = MatchType.Simple,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            var subDirs = Directory.EnumerateDirectories(WorkingFolder);
            foreach (var dir in subDirs)
            {
                var dirFiles = await Task.Run(() => Directory.EnumerateFiles(dir, "*.*", enumOptions).Where(x => filter(x)));
                foreach (var subFile in dirFiles)
                {
                    yield return subFile;
                }
            }

            var localFiles = Directory.EnumerateFiles(WorkingFolder, "*.*", localOpts).Where(x => filter(x));
            foreach (var file in localFiles)
            {
                yield return file;
            }
        }

        private static readonly EnumerationOptions enumOptions = new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.PlatformDefault,
            MatchType = MatchType.Simple,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };
    }

    public class DummyImageProvider : IImageProvider
    {
        public string WorkingFolder { get; set; }

        public async IAsyncEnumerable<string> GetFilesAsync(Predicate<string> filter, 
            [EnumeratorCancellation] CancellationToken token = default)
        {
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500, token);
                yield return $"File {i + 1}";
            }
        }
    }
}
