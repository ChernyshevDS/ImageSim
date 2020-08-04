using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Algorithms;
using ImageSim.Messages;
using ImageSim.Services;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImageSim.ViewModels
{
    public class MainVM : ViewModelBase
    {
        private readonly IFileService FileService;
        private readonly IFileDataStorage FileStorage;
        private readonly FileListVM FilesVM;
        private readonly IDialogCoordinator DialogService;

        private bool hasLoadedFiles;
        private RelayCommand addFromFolderCmd;
        private RelayCommand addFilesCmd;
        private RelayCommand dropFilesCommand;

        private RelayCommand compareHashesCommand;
        private RelayCommand checkSimilarDCTCmd;
        private RelayCommand clearCacheCmd;
        private RelayCommand syncCacheCmd;
        private RelayCommand<Uri> openLinkCmd;
        private TabVM currentTab;

        public RelayCommand AddFromFolderCommand => addFromFolderCmd ??= new RelayCommand(HandleAddFromFolder);
        public RelayCommand AddFilesCommand => addFilesCmd ??= new RelayCommand(HandleAddFiles);
        public RelayCommand DropFilesCommand => dropFilesCommand ??= new RelayCommand(DropAllFiles);

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes);
        public RelayCommand CheckSimilarDCTCommand => checkSimilarDCTCmd ??= new RelayCommand(HandleCompareDCTImageHashes);
        public RelayCommand ClearCacheCommand => clearCacheCmd ??= new RelayCommand(async () => await FileStorage.Invalidate());
        public RelayCommand SyncCacheCommand => syncCacheCmd ??= new RelayCommand(HandleSyncCache);

        public RelayCommand<Uri> OpenLinkCommand => openLinkCmd ??= new RelayCommand<Uri>(OpenLink);

        public bool HasLoadedFiles { get => hasLoadedFiles; set => Set(ref hasLoadedFiles, value); }

        public ObservableCollection<TabVM> Tabs { get; }
        public TabVM CurrentTab { get => currentTab; set => Set(ref currentTab, value); }

        public MainVM(IFileService fileService, FileListVM fileList, IFileDataStorage storage, IDialogCoordinator dial)
        {
            FileStorage = storage;
            FileService = fileService;
            FilesVM = fileList;
            DialogService = dial;

            var detailsTab = new TabVM()
            {
                Header = "Files",
                ContentVM = FilesVM,
                CanCloseTab = false
            };

            Tabs = new ObservableCollection<TabVM>() { detailsTab };

            ((INotifyPropertyChanged)FilesVM.LocatedFiles).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Count")
                {
                    HasLoadedFiles = (FilesVM.LocatedFiles.Count != 0);
                }
            };

            if (IsInDesignMode)
            {
                Tabs.Add(new TabVM() { Header = "Conflicts", ContentVM = new ConflictCollectionVM() });
                return;
            }

            Messenger.Default.Register<ConflictCollectionClearedMessage>(this, msg =>
            {
                var tab = Tabs.SingleOrDefault(x => x.ContentVM == msg.ConflictsVM);
                if (tab != null)
                    Messenger.Default.Send(new TabClosingMessage(tab));
            });

            Messenger.Default.Register<TabClosingMessage>(this, x =>
            {
                var tab = x.ClosingTab;
                Tabs.Remove(tab);
            });
        }

        private async void HandleAddFromFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select working folder",
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = FileService.EnumerateDirectory(dlg.SelectedPath, x => true);
                await AddFilesWithDialog(files);
            }
        }

        private async void HandleAddFiles()
        {
            var dlg = new OpenFileDialog() { Multiselect = true };
            if ((bool)dlg.ShowDialog())
            {
                await AddFilesWithDialog(dlg.FileNames);
            }
        }

        private async Task AddFilesWithDialog(IEnumerable<string> files)
        {
            var ctrl = await DialogService.ShowProgressAsync(this, "Adding files...", "Added 0 files", true);
            ctrl.SetIndeterminate();

            var ch = Channel.CreateUnbounded<string>();
            var producer = Task.Run(() =>
            {
                foreach (var file in files)
                {
                    if (ctrl.IsCanceled)
                        break;
                    ch.Writer.WriteAsync(file);
                }
                ch.Writer.Complete();
            });

            await Task.Yield();
            var added = 0;
            await foreach (var item in ch.Reader.ReadAllAsync())
            {
                FilesVM.AddFile(item);
                ctrl.SetMessage($"Added {++added} files");
            }

            if (ctrl.IsCanceled)
            {
                FilesVM.Clear();
            }

            await ctrl.CloseAsync();
        }

        private void DropAllFiles()
        {
            FilesVM.Clear();
            foreach (var tab in Tabs.Where(x => x.CanCloseTab))
            {
                tab.CloseTabCommand.Execute(null);
            }
        }

        private async void HandleCompareHashes()
        {
            var result = await HandleCompareHashesAsync();

            if (result.IsCancelled)
                return;

            if (result.Result == null)
            {
                await DialogService.ShowMessageAsync(this, "Hooray!", "There are no files with same hash!");
                return;
            }

            var tab = new TabVM() { Header = "Hash conflicts", ContentVM = result.Result };
            this.Tabs.Add(tab);
            CurrentTab = tab;
        }

        private async Task<OperationResult<ConflictCollectionVM>> HandleCompareHashesAsync()
        {
            var ctrl = await DialogService.ShowProgressAsync(this, "Calculating hashes...", "Hashed 0 files", true);

            int processed = 0;
            var total = FilesVM.LocatedFiles.Count;
            var thread_count = Utils.GetRecommendedConcurrencyLevel();

            var alg = new MD5FileSimilarityAlgorithm();
            var cache = new PersistentCacheService<MD5HashDescriptor>(FileStorage, alg.Name);

            var results = await TaskExtensions.ForEachAsync(FilesVM.LocatedFiles, x => Task.Run(() =>
            {
                if (ctrl.IsCanceled)
                    return false;

                if (!cache.TryGetValue(x, out MD5HashDescriptor hash))
                {
                    hash = alg.GetDescriptor(x);
                    cache.Add(x, hash);
                }

                var proc = Interlocked.Increment(ref processed);

                var progress = (double)proc / total;
                ctrl.SetProgress(progress);
                ctrl.SetMessage($"Hashed {proc} of {total}");
                return true;
            }), thread_count);

            if (ctrl.IsCanceled)
            {
                await ctrl.CloseAsync();
                return new OperationResult<ConflictCollectionVM>(true, null);
            }

            ctrl.SetIndeterminate();
            ctrl.SetMessage("Searching for hash conflicts...");

            var groups = cache
                .GroupBy(x => x.Value, new MD5HashComparer())
                .Where(x => x.Count() > 1)
                .ToList();

            if (!groups.Any())
            {
                await ctrl.CloseAsync();
                return new OperationResult<ConflictCollectionVM>(false, null);
            }

            ctrl.SetMessage("Building the view...");
            var coll = new ConflictCollectionVM();
            foreach (var group in groups)
            {
                var conflict = HashConflictVM.FromPaths(group.Select(x => x.Key));
                coll.Conflicts.Add(conflict);
            }

            await ctrl.CloseAsync();
            return new OperationResult<ConflictCollectionVM>(false, coll);
        }

        private IEnumerable<(T, T)> EnumeratePairs<T>(IReadOnlyList<T> source)
        {
            var len = source.Count;
            for (int i = 0; i < len - 1; i++)
            {
                for (int j = i + 1; j < len; j++)
                {
                    yield return (source[i], source[j]);
                }
            }
        }

        private async void HandleCompareDCTImageHashes()
        {           
            var ctrl = await DialogService.ShowProgressAsync(this, "Matching images...", "Preparing...", true);
            var algorithm = new DCTImageSimilarityAlgorithm();
            algorithm.Options.ClampSize = new Size(512, 512);
            var cachedAlg = algorithm.WithPersistentCache(FileStorage);

            var images = FilesVM.LocatedFiles.Where(x => VMHelper.IsImageExtension(Path.GetExtension(x))).ToList();
            var result = await BuildConflictsVM(ctrl, images, cachedAlg, 0.75);
            await ctrl.CloseAsync();

            if (result.IsCancelled)
                return;

            if (result.Result == null)
            {
                await DialogService.ShowMessageAsync(this, "Hooray!", "There are no files similar enough!");
                return;
            }

            var tab = new TabVM() { Header = "DCT", ContentVM = result.Result };
            this.Tabs.Add(tab);
            CurrentTab = tab;
        }

        readonly struct SimilarityIdx
        {
            internal readonly string Left;
            internal readonly string Right;
            internal readonly double Similarity;

            internal SimilarityIdx(string left, string right, double metric)
            {
                Left = left;
                Right = right;
                Similarity = metric;
            }
        }

        private async Task<OperationResult<ConflictCollectionVM>> BuildConflictsVM(ProgressDialogController ctrl, 
            IReadOnlyList<string> paths, ISimilarityAlgorithm similarityAlg, double threshold)
        {
            var pairs = EnumeratePairs(paths);
            var ch = Channel.CreateUnbounded<(string, string)>(new UnboundedChannelOptions() 
            { 
                SingleWriter = true, 
                SingleReader = false 
            });

            var N = paths.Count - 1;
            if (N <= 0)
                throw new ArgumentException("Not enough files provided", nameof(paths));
            
            var nPairs = N * (N + 1) / 2;
            var nProcessed = 0;

            var nReaders = Utils.GetRecommendedConcurrencyLevel();
            var nTasks = nReaders + 1;
            var tasks = new Task[nTasks];

            var bag = new ConcurrentBag<SimilarityIdx>();

            for (int i = 0; i < nReaders; i++)
            {
                tasks[i] = Task.Run(async () => {
                    while (await ch.Reader.WaitToReadAsync())
                    {
                        var pair = await ch.Reader.ReadAsync();
                        var metric = similarityAlg.GetSimilarity(pair.Item1, pair.Item2);
                        bag.Add(new SimilarityIdx(pair.Item1, pair.Item2, metric));

                        var proc = Interlocked.Increment(ref nProcessed);
                        var progress = (double)proc / nPairs;
                        ctrl.SetMessage($"Processed {proc} of {nPairs} image pairs");
                        ctrl.SetProgress(progress);
                    }
                });
            }
            
            var producer = Task.Run(() =>
            {
                foreach (var pair in pairs)
                {
                    if (ctrl.IsCanceled)
                    {
                        ch.Writer.Complete();
                        return;
                    }
                    ch.Writer.WriteAsync(pair);
                }
                ch.Writer.Complete();
            });
            
            tasks[nTasks - 1] = producer;
            await Task.WhenAll(tasks);

            if (ctrl.IsCanceled)
                return new OperationResult<ConflictCollectionVM>(true, null);

            ctrl.SetIndeterminate();
            ctrl.SetMessage("Sorting results by similarity metric...");
            var list = bag.ToList();
            list.Sort((x, y) => Math.Sign(y.Similarity - x.Similarity));    //sort by descending similarity
            
            ctrl.SetMessage("Building the view...");
            var cvm = new ConflictCollectionVM();
            foreach (var item in list)
            {
                if (item.Similarity < threshold)
                    break;  //as list is sorted, may break here

                cvm.Conflicts.Add(new ImageDCTConflictVM(item.Left, item.Right)
                {
                    SimilarityMetric = item.Similarity
                });
            }

            if (cvm.Conflicts.Count == 0)
                return new OperationResult<ConflictCollectionVM>(false, null);
            else
                return new OperationResult<ConflictCollectionVM>(false, cvm);
        }

        private async void HandleSyncCache()
        {
            var ctrl = await DialogService.ShowProgressAsync(this, "Compacting cache...", "Searching for orphaned cache records...");
            ctrl.SetProgress(0);

            var currentFolder = FilesVM.LocatedFiles.ToHashSet();
            var cached = FileStorage.GetAllKeys().ToList();

            var removedCnt = 0;
            var processed = 0;
            var total = cached.Count;
            foreach (var path in cached)
            {
                if (currentFolder.Contains(path))
                {
                    var record = await FileStorage.GetFileRecordAsync(path);
                    var realTime = PersistentFileRecord.ReadModificationTime(path);
                    if (record.Modified != realTime)
                    {
                        await FileStorage.RemoveFileRecordAsync(path);
                        ctrl.SetMessage($"Removed {++removedCnt} records");
                    }
                }
                else
                {
                    await FileStorage.RemoveFileRecordAsync(path);
                    ctrl.SetMessage($"Removed {++removedCnt} records");
                }
                var progress = ++processed / (double)total;
                ctrl.SetProgress(progress);
            }
            await ctrl.CloseAsync();

            await DialogService.ShowMessageAsync(this, "Complete", $"Removed {removedCnt} orphaned and outdated cache records");
        }

        private void OpenLink(Uri link)
        {
            Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true, CreateNoWindow = true });
        }
    }

    public class FileListVM : ViewModelBase
    {
        private readonly IFileService FileService;
        private readonly ObservableCollection<string> Files = new ObservableCollection<string>();
        
        private RelayCommand<string> deleteFileCommand;
        private RelayCommand<string> excludeFileCommand;
        private string selectedFile;
        private ViewModelBase fileDetailsVM = new EmptyDetailsVM();

        public ReadOnlyObservableCollection<string> LocatedFiles { get; }

        public RelayCommand<string> DeleteFileCommand => deleteFileCommand ??= new RelayCommand<string>(DeleteFile);
        public RelayCommand<string> ExcludeFileCommand => excludeFileCommand ??= new RelayCommand<string>(ExcludeFile);

        public string SelectedFile
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

        public ViewModelBase FileDetailsVM { get => fileDetailsVM; set => Set(ref fileDetailsVM, value); }

        public FileListVM(IFileService files)
        {
            this.FileService = files;
            LocatedFiles = new ReadOnlyObservableCollection<string>(Files);

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    Files.Add("File " + (i + 1));
                }
                return;
            }

            Messenger.Default.Register<CurrentFileChangedMessage>(this, x =>
            {
                FileDetailsVM = VMHelper.GetDetailsVMByPath(x?.File);
            }, true);

            Messenger.Default.Register<FileOperationMessage>(this, msg =>
            {
                switch (msg.Action)
                {
                    case FileOperation.Exclude:
                        ExcludeFile(msg.FilePath);
                        break;
                    case FileOperation.Delete:
                        DeleteFile(msg.FilePath);
                        break;
                    default:
                        break;
                }
            });
        }

        private void ExcludeFile(string path)
        {
            var idx = LocatedFiles.IndexOf(path);
            Files.Remove(path);
            idx = idx.Clamp(0, LocatedFiles.Count - 1);
            if(idx < LocatedFiles.Count)
                SelectedFile = LocatedFiles[idx];

            Messenger.Default.Send(new FileRemovedMessage(path));
        }

        private void DeleteFile(string obj)
        {
            ExcludeFile(obj);
            try
            {
                FileService.DeleteFileToBin(obj);
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public bool AddFile(string path)
        {
            if (Files.Contains(path))
                return false;
            Files.Add(path);
            return true;
        }

        public void Clear()
        {
            this.Files.Clear();
            Messenger.Default.Send(new FileListResetMessage());
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
                if (ConflictingFiles.Count == 1)
                {
                    MarkAsResolved();
                }
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
        private Lazy<ImageDetailsVM> firstImage;
        private Lazy<ImageDetailsVM> secondImage;
        private double similarityMetric;

        public double SimilarityMetric { get => similarityMetric; set => Set(ref similarityMetric, value); }
        public ImageDetailsVM FirstImage 
        { 
            get => firstImage.Value; 
        }
        public ImageDetailsVM SecondImage 
        { 
            get => secondImage.Value; 
        }
        public RelayCommand<ImageDetailsVM> KeepImageCommand => keepImageCommand
            ??= new RelayCommand<ImageDetailsVM>(HandleKeepImage);

        public ImageDCTConflictVM(string firstPath, string secondPath)
        {
            firstImage = new Lazy<ImageDetailsVM>(() => CreateDetails(firstPath));
            secondImage = new Lazy<ImageDetailsVM>(() => CreateDetails(secondPath));

            Messenger.Default.Register<FileRemovedMessage>(this, msg =>
            {
                if (msg.Path == firstPath || msg.Path == secondPath)
                {
                    MarkAsResolved();
                }
            }, true);
        }

        private ImageDetailsVM CreateDetails(string path) => 
            (ImageDetailsVM)VMHelper.GetDetailsVMByPath(path);

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
        private bool isLastConflict;

        public RelayCommand ResolveCommand => markResolvedCmd ??= new RelayCommand(MarkAsResolved);

        public bool IsLastConflict { get => isLastConflict; set => Set(ref isLastConflict, value); }

        public void MarkAsResolved()
        {
            Messenger.Default.Send(new ConflictResolvedMessage(this));
        }
    }
}
