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
        private readonly IExternalProgressIndicator ExternalProgress;

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

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes, HasFiles);
        public RelayCommand CheckSimilarDCTCommand => checkSimilarDCTCmd ??= new RelayCommand(HandleCompareDCTImageHashes, HasFiles);
        public RelayCommand ClearCacheCommand => clearCacheCmd ??= new RelayCommand(async () => await FileStorage.Invalidate());
        public RelayCommand SyncCacheCommand => syncCacheCmd ??= new RelayCommand(HandleSyncCache, HasFiles);

        public RelayCommand<Uri> OpenLinkCommand => openLinkCmd ??= new RelayCommand<Uri>(OpenLink);

        public bool HasLoadedFiles { get => hasLoadedFiles; set => Set(ref hasLoadedFiles, value); }

        public ObservableCollection<TabVM> Tabs { get; }
        public TabVM CurrentTab { get => currentTab; set => Set(ref currentTab, value); }

        public MainVM(IFileService fileService, FileListVM fileList, IFileDataStorage storage, 
            IDialogCoordinator dial, IExternalProgressIndicator externalProgress)
        {
            FileStorage = storage;
            FileService = fileService;
            FilesVM = fileList;
            DialogService = dial;
            ExternalProgress = externalProgress;

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
                    CommandManager.InvalidateRequerySuggested();
                }
            };

            if (IsInDesignMode)
            {
                Tabs.Add(new TabVM() { Header = "Conflicts", ContentVM = new HashConflictCollectionVM() });
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

        private async Task LinkExternalProgress(ProgressDialogController ctrl)
        {
            var pdlg = await DialogService.GetCurrentDialogAsync<ProgressDialog>(this);
            if (pdlg == null)
                return;
            if (!(pdlg.FindName("PART_ProgressBar") is System.Windows.Controls.ProgressBar bar))
            {
                ExternalProgress.SetState(ExternalIndicatorState.None);
                return;
            }

            ExternalProgress.SetState(ExternalIndicatorState.Normal);
            ExternalProgress.SetProgress(0);
            ThreadPool.QueueUserWorkItem(async _ => {
                while (ctrl.IsOpen)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        ExternalProgress.SetState(bar.IsIndeterminate ? ExternalIndicatorState.Indeterminate 
                                                                      : ExternalIndicatorState.Normal);
                        ExternalProgress.SetProgress(bar.Value);
                    });
                    await Task.Delay(100);
                }

                Application.Current.Dispatcher.Invoke(() => {
                    ExternalProgress.SetState(ExternalIndicatorState.None);
                });
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
            await LinkExternalProgress(ctrl);
            ctrl.SetIndeterminate();

            var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(20)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });
            var producer = Task.Run(async () =>
            {
                foreach (var file in files)
                {
                    if (ctrl.IsCanceled)
                        break;
                    await ch.Writer.WriteAsync(file);
                }
                ch.Writer.Complete();
            });

            var added = 0;
            await foreach (var item in ch.Reader.ReadAllAsync())
            {
                await FilesVM.AddFileAsync(item);
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

        private bool HasFiles()
        {
            return FilesVM.LocatedFiles.Count > 1;
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

        private async Task<OperationResult<HashConflictCollectionVM>> HandleCompareHashesAsync()
        {
            var ctrl = await DialogService.ShowProgressAsync(this, "Calculating hashes...", "Hashed 0 files", true);
            await LinkExternalProgress(ctrl);

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
                return new OperationResult<HashConflictCollectionVM>(true, null);
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
                return new OperationResult<HashConflictCollectionVM>(false, null);
            }

            ctrl.SetMessage("Building the view...");
            var coll = new HashConflictCollectionVM();
            foreach (var group in groups)
            {
                var conflict = new HashConflictDescriptor(group.Select(x => x.Key).ToArray(), group.Key.Data);
                coll.AddConflict(conflict);
            }

            await ctrl.CloseAsync();
            return new OperationResult<HashConflictCollectionVM>(false, coll);
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
            await LinkExternalProgress(ctrl);

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

        private async Task<OperationResult<ImageConflictCollectionVM>> BuildConflictsVM(ProgressDialogController ctrl, 
            IReadOnlyList<string> paths, ISimilarityAlgorithm similarityAlg, double threshold)
        {
            var nReaders = Utils.GetRecommendedConcurrencyLevel();
            var pairs = EnumeratePairs(paths);
            var ch = Channel.CreateBounded<(string, string)>(new BoundedChannelOptions(nReaders * 2) 
            { 
                SingleWriter = true, 
                SingleReader = false, 
                FullMode = BoundedChannelFullMode.Wait
            });

            var progressChannel = Channel.CreateBounded<int>(new BoundedChannelOptions(nReaders) 
            { 
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            var N = paths.Count - 1;
            if (N <= 0)
                throw new ArgumentException("Not enough files provided", nameof(paths));
            
            var nPairs = N * (N + 1) / 2;
            var nProcessed = 0;
            var nRunningReaders = 0;

            var tasks = new List<Task>(nReaders);

            var bag = new ConcurrentBag<ConflictDescriptor>();

            for (int i = 0; i < nReaders; i++)
            {
                var readTask = Task.Run(async () => {
                    var nRun = Interlocked.Increment(ref nRunningReaders);
                    //System.Diagnostics.Debug.WriteLine($"Started: {nRun}");
                    await foreach (var pair in ch.Reader.ReadAllAsync())
                    {
                        var metric = similarityAlg.GetSimilarity(pair.Item1, pair.Item2);
                        if(metric >= threshold)
                            bag.Add(new ConflictDescriptor(pair.Item1, pair.Item2, metric));

                        var proc = Interlocked.Increment(ref nProcessed);
                        await progressChannel.Writer.WriteAsync(proc);
                    }

                    var running = Interlocked.Decrement(ref nRunningReaders);
                    //System.Diagnostics.Debug.WriteLine($"Running: {running}");
                    if (running == 0)
                    {
                        progressChannel.Writer.Complete();
                    }
                });
                tasks.Add(readTask);
            }

            var reporter = Task.Run(async () => 
            {
                while (await progressChannel.Reader.WaitToReadAsync())
                {
                    var proc = await progressChannel.Reader.ReadAsync();
                    if (proc < N)
                    {
                        var progress = (double)proc / N;
                        ctrl.SetMessage($"Hashed {proc} of {N} images");
                        ctrl.SetProgress(progress);
                    }
                    else
                    {
                        var progress = (double)proc / nPairs;
                        ctrl.SetMessage($"Processed {proc} of {nPairs} image pairs");
                        ctrl.SetProgress(progress);
                    }
                    await Task.Delay(100);
                }
                await Task.Delay(100);
            });

            var producer = Task.Run(async () =>
            {
                foreach (var pair in pairs)
                {
                    if (ctrl.IsCanceled)
                    {
                        ch.Writer.Complete();
                        return;
                    }
                    await ch.Writer.WriteAsync(pair);
                }
                ch.Writer.Complete();
            });

            tasks.Add(producer);
            tasks.Add(reporter);
            await Task.WhenAll(tasks.ToArray());

            if (ctrl.IsCanceled)
                return new OperationResult<ImageConflictCollectionVM>(true, null);

            ctrl.SetIndeterminate();
            ctrl.SetMessage("Sorting results by similarity metric...");
            var list = bag.ToList();
            list.Sort((x, y) => Math.Sign(y.Similarity - x.Similarity));    //sort by descending similarity
            
            ctrl.SetMessage("Building the view...");            
            var cvm = new ImageConflictCollectionVM(list);
            
            if (cvm.ConflictsCount == 0)
                return new OperationResult<ImageConflictCollectionVM>(false, null);
            else
                return new OperationResult<ImageConflictCollectionVM>(false, cvm);
        }

        private async void HandleSyncCache()
        {
            var ctrl = await DialogService.ShowProgressAsync(this, "Compacting cache...", "Searching for orphaned cache records...");
            await LinkExternalProgress(ctrl);
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
}
