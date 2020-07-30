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
    public class FileRegistry : IFileRegistry
    {
        public IEnumerable<string> GetFiles(string folder, Predicate<string> filter)
        {
            if (!Directory.Exists(folder))
                yield break;

            var localOpts = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.PlatformDefault,
                MatchType = MatchType.Simple,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            var subDirs = Directory.EnumerateDirectories(folder);
            foreach (var dir in subDirs)
            {
                var dirFiles = Directory.EnumerateFiles(dir, "*", enumOptions).Where(x => filter(x));
                foreach (var subFile in dirFiles)
                {
                    yield return subFile;
                }
            }

            var localFiles = Directory.EnumerateFiles(folder, "*", localOpts).Where(x => filter(x));
            foreach (var file in localFiles)
            {
                yield return file;
            }
        }

        public async IAsyncEnumerable<string> GetFilesAsync(string folder, Predicate<string> filter, 
            [EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield();

            if (!Directory.Exists(folder))
                yield break;

            var localOpts = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.PlatformDefault,
                MatchType = MatchType.Simple,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            var subDirs = Directory.EnumerateDirectories(folder);
            foreach (var dir in subDirs)
            {
                var dirFiles = Directory.EnumerateFiles(dir, "*", enumOptions).Where(x => filter(x));
                foreach (var subFile in dirFiles)
                {
                    yield return subFile;
                }
            }

            var localFiles = Directory.EnumerateFiles(folder, "*", localOpts).Where(x => filter(x));
            foreach (var file in localFiles)
            {
                yield return file;
            }
        }

        public void DeleteFileToBin(string path)
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
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

    public class DummyFileRegistry : IFileRegistry
    {
        public void DeleteFileToBin(string path) => throw new NotImplementedException();

        public async IAsyncEnumerable<string> GetFilesAsync(string folder, Predicate<string> filter, 
            [EnumeratorCancellation] CancellationToken token = default)
        {
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500, token);
                yield return $"File {i + 1}";
            }
        }
    }

    public class FileSystemCrawlerSO
    {
        public int NumFolders { get; set; }
        private readonly System.Collections.Concurrent.ConcurrentBag<Task> tasks 
            = new System.Collections.Concurrent.ConcurrentBag<Task>();

        public void CollectFolders(string path)
        {

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            tasks.Add(Task.Run(() => CrawlFolder(directoryInfo)));

            Task taskToWaitFor;
            while (tasks.TryTake(out taskToWaitFor))
                taskToWaitFor.Wait();
        }


        private void CrawlFolder(DirectoryInfo dir)
        {
            try
            {
                DirectoryInfo[] directoryInfos = dir.GetDirectories();
                foreach (DirectoryInfo childInfo in directoryInfos)
                {
                    // here may be dragons using enumeration variable as closure!!
                    DirectoryInfo di = childInfo;
                    tasks.Add(Task.Run(() => CrawlFolder(di)));
                }
                // Do something with the current folder
                // e.g. Console.WriteLine($"{dir.FullName}");
                NumFolders++;
            }
            catch (Exception ex)
            {
                while (ex != null)
                {
                    Console.WriteLine($"{ex.GetType()} {ex.Message}\n{ex.StackTrace}");
                    ex = ex.InnerException;
                }
            }
        }
    }
}
