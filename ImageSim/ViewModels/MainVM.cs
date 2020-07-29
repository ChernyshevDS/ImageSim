using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using ImageSim.Services;
using ImageSim.Services.Storage;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageSim.ViewModels
{
    public class MainVM : ViewModelBase
    {
        private readonly IImageProvider ImageProvider;
        private readonly IFileDataStorage FileStorage;

        private RelayCommand addFromFolderCmd;
        private RelayCommand addFilesCmd;

        private RelayCommand compareHashesCommand;
        private RelayCommand checkSimilarDCTCmd;
        private RelayCommand clearCacheCmd;
        private RelayCommand syncCacheCmd;
        
        private GenericFileVM selectedFile;

        public RelayCommand AddFromFolderCommand => addFromFolderCmd ??= new RelayCommand(HandleAddFromFolder);
        public RelayCommand AddFilesCommand => addFilesCmd ??= new RelayCommand(HandleAddFiles);

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes);
        public RelayCommand CheckSimilarDCTCommand => checkSimilarDCTCmd ??= new RelayCommand(HandleCompareDCTImageHashes);
        public RelayCommand ClearCacheCommand => clearCacheCmd ??= new RelayCommand(async () => await FileStorage.Invalidate());
        public RelayCommand SyncCacheCommand => syncCacheCmd ??= new RelayCommand(HandleSyncCache);

        public ObservableCollection<GenericFileVM> LocatedFiles { get; }
        public ObservableCollection<TabVM> Tabs { get; }
        
        public GenericFileVM SelectedFile
        {
            get => selectedFile;
            set
            {
                if (Set(ref selectedFile, value))
                {
                    Messenger.Default.Send(new CurrentFileChangedMessage(value));
                }
            }
        }

        public MainVM(IImageProvider imageProvider, IFileDataStorage storage)
        {
            ImageProvider = imageProvider;
            FileStorage = storage;
            LocatedFiles = new ObservableCollection<GenericFileVM>();

            var detailsTab = new TabVM() 
            {
                Header = "Current file",
                ContentVM = new EmptyDetailsVM(),
                CanCloseTab = false
            };
            Tabs = new ObservableCollection<TabVM>() { detailsTab };

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    LocatedFiles.Add(new GenericFileVM() { FilePath = "File " + (i + 1) });
                }

                Tabs.Add(new TabVM() { Header = "Conflicts", ContentVM = new ConflictCollectionVM() });
                return;
            }

            Messenger.Default.Register<CurrentFileChangedMessage>(this, x =>
            {
                detailsTab.ContentVM = VMHelper.GetDetailsVMByPath(x?.File?.FilePath);
            }, true);

            Messenger.Default.Register<FileOperationMessage>(this, msg => {
                try
                {
                    switch (msg.Action)
                    {
                        case FileOperation.Exclude:
                            ExcludeFile(msg.FilePath);
                            break;
                        case FileOperation.Delete:
                            ExcludeFile(msg.FilePath);
                            File.Delete(msg.FilePath);
                            break;
                        default:
                            break;
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });

            Messenger.Default.Register<TabClosingMessage>(this, x => {
                var tab = x.ClosingTab;
                Tabs.Remove(tab);
                GC.Collect();   //FIXME
            });           
        }

        private void ExcludeFile(string path)
        {
            var fvm = LocatedFiles.SingleOrDefault(x => x.FilePath == path);
            var idx = LocatedFiles.IndexOf(fvm);
            LocatedFiles.Remove(fvm);
            idx = idx.Clamp(0, LocatedFiles.Count - 1);
            SelectedFile = LocatedFiles[idx];

            Messenger.Default.Send(new FileRemovedMessage(path));
        }

        private void HandleAddFromFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select working folder",
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var result = ProgressWindow.RunTaskAsync(async (progress, token) => {
                    int added = 0;
                    await foreach (var file in ImageProvider.GetFilesAsync(dlg.SelectedPath, x => true))
                    {
                        if (LocatedFiles.Any(x => x.FilePath == file))
                            continue;

                        LocatedFiles.Add(new GenericFileVM() { FilePath = file });
                        added++;
                        progress.Report(new ProgressArgs($"Added {added} files", null));
                    }
                    return added;
                }, "Adding files...", false);
            }
        }

        private void HandleAddFiles()
        {
            var dlg = new OpenFileDialog() { Multiselect = true };
            if ((bool)dlg.ShowDialog())
            {
                foreach (var file in dlg.FileNames)
                {
                    if (LocatedFiles.Any(x => x.FilePath == file))
                        continue;

                    LocatedFiles.Add(new GenericFileVM() { FilePath = file });
                }
            }
        }

        private void HandleCompareHashes()
        {
            var result = ProgressWindow.RunTaskAsync(
                async (prog, tok) => await Task.Run(() => RunFilesHashingAsync(prog, tok)),
                "Hashing...", true);

            if (result.IsCancelled)
                return;

            if (result.Result == null)
            {
                MessageBox.Show("No conflicts found!");
                return;
            }
            
            this.Tabs.Add(new TabVM() { Header = "Hash conflicts", ContentVM = result.Result });
        }

        private void HandleCompareDCTImageHashes()
        {
            var result = ProgressWindow.RunTaskAsync(
                async (prog, tok) => await Task.Run(() => RunFilesDCTHashingAsync(prog, tok)), 
                "DCT hashing...", true);
            
            if (result.IsCancelled)
                return;

            if (result.Result == null || result.Result.Conflicts.Count == 0)
            {
                MessageBox.Show("No conflicts found!");
                return;
            }

            this.Tabs.Add(new TabVM() { Header = "DCT", ContentVM = result.Result });
        }

        private async Task<T> GetOrCreateAssociatedFileData<T>(string path, string key, Func<string, T> generator) where T : IFileRecordData, new()
        {
            var needCacheUpdate = false;
            var cachedRecord = await FileStorage.GetFileRecordAsync(path);
            if (cachedRecord == null)   //no cache record found
                cachedRecord = PersistentFileRecord.Create(path);

            T filedata = default;
            var time = PersistentFileRecord.ReadModificationTime(path);
            if (time.HasValue)
            {
                if (cachedRecord.Modified == time)   //cached record is valid - file hasn't been changed
                {
                    if (cachedRecord.TryGetData<T>(key, out T data))   //success - use cached value
                    {
                        filedata = data;
                        //System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(path)}: loaded from cache");
                    }
                    else    //no cached Hash
                    {
                        filedata = generator(path);
                        //System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(path)}: no cached hash, calculated");
                        needCacheUpdate = true;
                    }
                }
                else    //cached value expired
                {
                    await FileStorage.RemoveFileRecordAsync(path);
                    filedata = generator(path);
                    //System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(path)}: file modified, hash calculated");
                    needCacheUpdate = true;
                }
            } // else current file can't be read - skip

            if (needCacheUpdate)
            {
                cachedRecord.SetData(filedata);
                await FileStorage.UpdateFileRecordAsync(path, cachedRecord);
                System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(path)}: cache updated");
            }

            return filedata;
        }

        private async Task<ConflictCollectionVM> RunFilesDCTHashingAsync(IProgress<ProgressArgs> progress, CancellationToken token)
        {
            int processed = 0;
            var total = LocatedFiles.Count;
            var maxSize = new OpenCvSharp.Size(512, 512);

            var dict = new ConcurrentDictionary<GenericFileVM, ulong>(4, total);
            var results = await TaskExtensions.ForEachAsync(LocatedFiles, async (x) =>
            {
                token.ThrowIfCancellationRequested();

                if (!VMHelper.IsImageExtension(Path.GetExtension(x.FilePath)))
                    return false;

                var data = await GetOrCreateAssociatedFileData(x.FilePath, DCTImageHashData.Key,
                    p => new DCTImageHashData() { Hash = PHash.DCT.GetImageHash(p, maxSize) });
                dict.TryAdd(x, data.Hash);

                var proc = Interlocked.Increment(ref processed);
                progress.Report(new ProgressArgs($"Hashed {proc} of {total}", proc * 100.0 / total));
                return true;
            }, 4);

            var max_similarity = 10;
            progress.Report(new ProgressArgs($"Searching similarities", null));
            var hashes = dict.ToList();
            var cvm = new ConflictCollectionVM();
            for (int i = 0; i < dict.Count - 1; i++)
            {
                for (int j = i + 1; j < dict.Count; j++)
                {
                    var left = hashes[i];
                    var right = hashes[j];
                    var distance = PHash.DCT.HammingDistance(left.Value, right.Value);
                    if (distance <= max_similarity)
                    {
                        cvm.Conflicts.Add(new ImageDCTConflictVM() 
                        { 
                            FirstImage = (ImageDetailsVM)VMHelper.GetDetailsVMByPath(left.Key.FilePath),
                            SecondImage = (ImageDetailsVM)VMHelper.GetDetailsVMByPath(right.Key.FilePath)
                        });
                    }
                }
            }

            if (cvm.Conflicts.Count == 0)
                return null;

            return cvm;
        }

        private void HandleSyncCache()
        {
            var removed = ProgressWindow.RunTaskAsync(async (progress, token) => {
                progress.Report(new ProgressArgs("Searching orphaned cache records...", null));
                var currentFolder = LocatedFiles.Select(x => x.FilePath).ToHashSet();
                var cached = FileStorage.GetAllKeys();

                var removedCnt = 0;
                foreach (var path in cached)
                {
                    if (currentFolder.Contains(path))
                    {
                        var record = await FileStorage.GetFileRecordAsync(path);
                        var realTime = PersistentFileRecord.ReadModificationTime(path);
                        if (record.Modified != realTime)
                        {
                            await FileStorage.RemoveFileRecordAsync(path);
                            progress.Report(new ProgressArgs($"Removed {++removedCnt} records", null));
                        }
                    }
                    else 
                    {
                        await FileStorage.RemoveFileRecordAsync(path);
                        progress.Report(new ProgressArgs($"Removed {++removedCnt} records", null));
                    }
                }

                return removedCnt;
            }, "Synchronizing cache...");
            MessageBox.Show($"Removed {removed.Result} orphaned and outdated cache records");
        }

        private async Task<ConflictCollectionVM> RunFilesHashingAsync(IProgress<ProgressArgs> progress, CancellationToken token)
        {
            int processed = 0;
            var total = LocatedFiles.Count;
            var thread_count = Utils.GetRecommendedConcurrencyLevel();

            var hashDict = new ConcurrentDictionary<string, string>(thread_count, total);
            var results = await TaskExtensions.ForEachAsync(LocatedFiles, async (x) =>
            {
                token.ThrowIfCancellationRequested();

                var data = await GetOrCreateAssociatedFileData(x.FilePath, HashData.Key,
                    p => new HashData() { Hash = Utils.GetFileHash(p) });
                hashDict.TryAdd(x.FilePath, data.Hash);

                var proc = Interlocked.Increment(ref processed);
                progress.Report(new ProgressArgs($"Hashed {proc} of {total}", proc * 100.0 / total));
                return true;
            }, thread_count);

            var groups = hashDict
                .GroupBy(x => x.Value)
                .Where(x => x.Count() > 1)
                .ToList();

            if (!groups.Any())
                return null;

            var coll = new ConflictCollectionVM();
            foreach (var group in groups)
            {
                var conflict = HashConflictVM.FromPaths(group.Select(x => x.Key));
                coll.Conflicts.Add(conflict);
            }

            return coll;
        }
    }

    public static class VMHelper
    {
        private static readonly HashSet<string> image_extensions = new HashSet<string>()
        {
            "JPG", "JPEG", "TIFF", "PNG", "BMP", "EMF", "EXIF", "ICO", "WMF"
        };

        public static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext))
                return false;
            return image_extensions.Contains(ext.TrimStart('.').ToUpperInvariant());
        }

        public static ViewModelBase GetDetailsVMByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new EmptyDetailsVM();
            var ext = Path.GetExtension(path);
            if (IsImageExtension(ext))
                return new ImageDetailsVM(path);
            else
                return new FileDetailsVM(path);
        }
    }

    public class EmptyDetailsVM : ViewModelBase
    {
    }

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

    public class ImageDetailsVM : FileDetailsVM
    {
        private int width;
        private int height;
        private bool isValid;
        private string format;

        public int Width { get => width; set => Set(ref width, value); }
        public int Height { get => height; set => Set(ref height, value); }
        public string Format { get => format; set => Set(ref format, value); }
        public bool IsValid { get => isValid; set => Set(ref isValid, value); }

        public ImageDetailsVM(string path) : base(path)
        {
            try
            {
                using var img = System.Drawing.Image.FromFile(FilePath);
                Width = img.Width;
                Height = img.Height;
                Format = img.RawFormat.ToString();
                IsValid = true;
            }
            catch (Exception)
            {
                Width = 0;
                Height = 0;
                Format = "Unknown format";
                IsValid = false;
            }
        }
    }

    public class HashConflictVM : ConflictVM
    {
        private ViewModelBase detailsVM;
        private HashConflictEntryVM selectedFile;

        public ViewModelBase DetailsVM { get => detailsVM; set => Set(ref detailsVM, value); }
        public HashConflictEntryVM SelectedFile
        {
            get => selectedFile;
            set
            {
                if (Set(ref selectedFile, value))
                {
                    DetailsVM = VMHelper.GetDetailsVMByPath(selectedFile?.FilePath);
                }
            }
        }

        public ObservableCollection<HashConflictEntryVM> ConflictingFiles { get; }

        public HashConflictVM()
        {
            ConflictingFiles = new ObservableCollection<HashConflictEntryVM>();
            ConflictingFiles.CollectionChanged += ConflictingFiles_CollectionChanged;

            Messenger.Default.Register<FileRemovedMessage>(this, HandleFileRemoved);
        }

        private void ConflictingFiles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldItem = e.OldItems?.OfType<HashConflictEntryVM>().FirstOrDefault();
            var newItem = e.NewItems?.OfType<HashConflictEntryVM>().FirstOrDefault();

            if (SelectedFile == null || SelectedFile == oldItem)
            {
                SelectedFile = newItem ?? ConflictingFiles.FirstOrDefault();
            }
        }

        public static HashConflictVM FromPaths(IEnumerable<string> paths)
        {
            var vm = new HashConflictVM();
            foreach (var item in paths)
            {
                vm.ConflictingFiles.Add(new HashConflictEntryVM() { FilePath = item });
            }
            return vm;
        }

        private void HandleFileRemoved(FileRemovedMessage obj)
        {
            var entry = ConflictingFiles.FirstOrDefault(x => x.FilePath == obj.Path);
            if (entry != null)
            {
                ConflictingFiles.Remove(entry);
            }
        }
    }

    public class HashConflictEntryVM : ViewModelBase
    {
        private RelayCommand deleteCommand;
        private string filePath;

        public RelayCommand DeleteCommand => deleteCommand ??= new RelayCommand(HandleDelete);

        private void HandleDelete()
        {
            Messenger.Default.Send(new FileOperationMessage(FilePath, FileOperation.Delete));
        }

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
    }

    public class ImageDCTConflictVM : ConflictVM
    {
        private RelayCommand<ImageDetailsVM> keepImageCommand;
        private ImageDetailsVM firstImage;
        private ImageDetailsVM secondImage;

        public ImageDetailsVM FirstImage { get => firstImage; set => Set(ref firstImage, value); }
        public ImageDetailsVM SecondImage { get => secondImage; set => Set(ref secondImage, value); }
        public RelayCommand<ImageDetailsVM> KeepImageCommand => keepImageCommand 
            ??= new RelayCommand<ImageDetailsVM>(HandleKeepImage);

        public ImageDCTConflictVM()
        {
            Messenger.Default.Register<FileRemovedMessage>(this, msg => {
                if (msg.Path == FirstImage.FilePath || msg.Path == SecondImage.FilePath)
                {
                    MarkAsResolved();
                }
            });
        }

        private void HandleKeepImage(ImageDetailsVM obj)
        {
            var toDelete = obj.FilePath == FirstImage.FilePath 
                ? SecondImage.FilePath 
                : FirstImage.FilePath;
            Messenger.Default.Send(new FileOperationMessage(toDelete, FileOperation.Delete));
        }
    }

    public class ConflictVM : ViewModelBase
    {
        private RelayCommand markResolvedCmd;

        public RelayCommand ResolveCommand => markResolvedCmd ??= new RelayCommand(MarkAsResolved);

        public void MarkAsResolved()
        {
            Messenger.Default.Send(new ConflictResolvedMessage(this));
        }
    }
}
