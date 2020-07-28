﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using ImageSim.Services;
using ImageSim.Services.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private RelayCommand setWorkingFolderCmd;
        private RelayCommand reloadFilesCmd;
        private RelayCommand cancelFileSearchCmd;
        private RelayCommand compareHashesCommand;
        private RelayCommand checkSimilarDCTCmd;
        private RelayCommand clearCacheCmd;
        private RelayCommand syncCacheCmd;
        private bool isFileSearchInProgress = false;
        private CancellationTokenSource cancellationSource;
        private GenericFileVM selectedFile;

        public RelayCommand SetWorkingFolderCommand => setWorkingFolderCmd ??= new RelayCommand(HandleSetWorkingFolder);
        public RelayCommand ReloadFilesCommand => reloadFilesCmd ??= new RelayCommand(async () => await ReloadFiles());
        public RelayCommand CancelFileSearchCommand => cancelFileSearchCmd ??= new RelayCommand(HandleCancelSearch);

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes);
        public RelayCommand CheckSimilarDCTCommand => checkSimilarDCTCmd ??= new RelayCommand(HandleCompareDCTImageHashes);
        public RelayCommand ClearCacheCommand => clearCacheCmd ??= new RelayCommand(async () => await FileStorage.Invalidate());
        public RelayCommand SyncCacheCommand => syncCacheCmd ??= new RelayCommand(HandleSyncCache);

        public bool IsFileSearchInProgress { get => isFileSearchInProgress; set => Set(ref isFileSearchInProgress, value); }
        public ObservableCollection<GenericFileVM> LocatedFiles { get; }
        public ObservableCollection<TabVM> Tabs { get; }
        public string WorkingFolder
        {
            get => ImageProvider.WorkingFolder;
            set
            {
                if (!EqualityComparer<string>.Default.Equals(ImageProvider.WorkingFolder, value))
                {
                    ImageProvider.WorkingFolder = value;
                    RaisePropertyChanged();
                }
            }
        }
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

            Messenger.Default.Register<CurrentFileChangedMessage>(this, x =>
            {
                if (x.File == null)
                {
                    detailsTab.ContentVM = new EmptyDetailsVM();
                }
                else 
                {
                    detailsTab.ContentVM = VMHelper.GetDetailsVMByFileExtension(x.File);
                }
            }, true);

            Messenger.Default.Register<FileDeletingMessage>(this, msg => {
                try
                {
                    var fvm = LocatedFiles.SingleOrDefault(x => x.FilePath == msg.FilePath);
                    var idx = LocatedFiles.IndexOf(fvm);
                    LocatedFiles.Remove(fvm);
                    idx = idx.Clamp(0, LocatedFiles.Count - 1);
                    SelectedFile = LocatedFiles[idx];
                    File.Delete(msg.FilePath);
                }
                catch (IOException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });

            Messenger.Default.Register<TabClosingMessage>(this, x => {
                var tab = x.ClosingTab;
                Tabs.Remove(tab);
                GC.Collect();
            });

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    LocatedFiles.Add(new GenericFileVM() { FilePath = "File " + (i + 1), Hash = "ABCDEF" });
                }

                Tabs.Add(new TabVM() { Header = "Conflicts", ContentVM = new ConflictCollectionVM() });
            }
        }

        private void HandleSetWorkingFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select working folder",
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WorkingFolder = dlg.SelectedPath;
            }
        }

        private async Task ReloadFiles()
        {
            IsFileSearchInProgress = true;
            cancellationSource = new CancellationTokenSource();

            LocatedFiles.Clear();
            try
            {
                await foreach (var item in ImageProvider.GetFilesAsync(x => true).WithCancellation(cancellationSource.Token))
                {
                    LocatedFiles.Add(new GenericFileVM() { FilePath = item });
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Operation cancelled");
            }
            finally
            {
                IsFileSearchInProgress = false;
            }
        }

        private void HandleCancelSearch()
        {
            cancellationSource?.Cancel();
        }

        private void HandleCompareHashes()
        {
            var result = ProgressWindow.RunTaskAsync(RunFilesHashingAsync, "Hashing...", true);

            if (result.IsCancelled)
            {
                var doContinue = MessageBox.Show("Hashing cancelled. Try to find similar files anyway?", "Warning", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (doContinue != MessageBoxResult.Yes)
                    return;
            }

            var groups = LocatedFiles.Where(x => !string.IsNullOrEmpty(x.Hash))
                .GroupBy(x => x.Hash)
                .Where(x => x.Count() > 1)
                .ToList();

            if (!groups.Any())
            {
                MessageBox.Show("No conflicts found!");
                return;
            }

            var coll = new ConflictCollectionVM();
            foreach (var group in groups)
            {
                var conflict = new FileGroupVM(group);
                coll.Conflicts.Add(conflict);
            }
            this.Tabs.Add(new TabVM() { Header = "Hash conflicts", ContentVM = coll });
        }

        private void HandleCompareDCTImageHashes()
        {
            var result = ProgressWindow.RunTaskAsync(RunFilesDCTHashingAsync, "DCT hashing...", false);
            this.Tabs.Add(new TabVM() { Header = "DCT", ContentVM = result.Result });
        }

        private async Task<T> GetOrCreateAssociatedFileData<T>(string path, Func<string, T> generator) where T : IFileRecordData, new()
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
                    var key = typeof(T).GetProperty("Key").GetValue(null).ToString();
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
                if (!VMHelper.IsImageExtension(Path.GetExtension(x.FilePath)))
                    return false;

                var data = await GetOrCreateAssociatedFileData(x.FilePath, 
                    p => new DCTImageHashData() { Hash = PHash.DCT.GetImageHash(p, maxSize) });
                dict.TryAdd(x, data.Hash);

                var proc = Interlocked.Increment(ref processed);
                progress.Report(new ProgressArgs($"Hashed {proc} of {total}", proc * 100.0 / total));
                return true;
            }, 4);

            var max_similarity = 10;
            progress.Report(new ProgressArgs($"Searching similarities", null));
            var hashes = dict.ToList();
            var conflict = new List<GenericFileVM>(2) { null, null };
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
                        conflict[0] = left.Key;
                        conflict[1] = right.Key;
                        cvm.Conflicts.Add(new FileGroupVM(conflict));
                    }
                }
            }

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

        private async Task<int> RunFilesHashingAsync(IProgress<ProgressArgs> progress, CancellationToken token)
        {
            int processed = 0;
            var total = LocatedFiles.Count;

            var results = await TaskExtensions.ForEachAsync(LocatedFiles, async (x) =>
            {
                if (string.IsNullOrEmpty(x.Hash))
                {
                    var data = await GetOrCreateAssociatedFileData(x.FilePath, p => new HashData() { Hash = Utils.GetFileHash(p) });
                    x.Hash = data.Hash;
                }

                var proc = Interlocked.Increment(ref processed);
                progress.Report(new ProgressArgs($"Hashed {proc} of {total}", proc * 100.0 / total));
                return true;
            }, 4);

            return results.Count(x => x);
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
            return image_extensions.Contains(ext.TrimStart('.').ToUpperInvariant());
        }

        public static FileDetailsVM GetDetailsVMByFileExtension(GenericFileVM vm)
        {
            var ext = Path.GetExtension(vm.FilePath);
            if (IsImageExtension(ext))
                return new ImageDetailsVM(vm);
            else
                return new FileDetailsVM(vm);
        }
    }

    public class EmptyDetailsVM : ViewModelBase
    {
    }

    public class FileDetailsVM : ViewModelBase
    {
        private string filePath;
        private string hash;
        private long fileSize;
        private RelayCommand deleteCommand;

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public string Hash { get => hash; set => Set(ref hash, value); }
        public long FileSize { get => fileSize; set => Set(ref fileSize, value); }

        public RelayCommand DeleteCommand => deleteCommand ??= new RelayCommand(HandleDelete);

        private void HandleDelete()
        {
            Messenger.Default.Send(new FileDeletingMessage(FilePath));
        }

        public FileDetailsVM(GenericFileVM file)
        {
            FilePath = file.FilePath;
            Hash = file.Hash;

            FileInfo fi = new FileInfo(FilePath);
            FileSize = fi.Length;
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

        public ImageDetailsVM(GenericFileVM vm) : base(vm)
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

    public class FileGroupVM : ViewModelBase
    { 
        public ObservableCollection<FileDetailsVM> Files { get; }

        public FileGroupVM(IEnumerable<GenericFileVM> conflictingFiles)
        {
            Files = new ObservableCollection<FileDetailsVM>();
            foreach (var item in conflictingFiles)
            {
                var details = VMHelper.GetDetailsVMByFileExtension(item);
                Files.Add(details);
            }

            Messenger.Default.Register<FileDeletingMessage>(this, msg => {
                var toRemove = Files.SingleOrDefault(z => z.FilePath == msg.FilePath);
                Files.Remove(toRemove);

                if (Files.Count <= 1)
                {
                    Messenger.Default.Send(new ConflictResolvedMessage(this));
                }
            });
        }
    }
}
