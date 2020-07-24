using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using ImageSim.Services;
using ImageSim.Services.Storage;
using System;
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
        private RelayCommand clearCacheCmd;
        private bool isFileSearchInProgress = false;
        private CancellationTokenSource cancellationSource;
        private GenericFileVM selectedFile;

        public RelayCommand SetWorkingFolderCommand => setWorkingFolderCmd ??= new RelayCommand(HandleSetWorkingFolder);
        public RelayCommand ReloadFilesCommand => reloadFilesCmd ??= new RelayCommand(async () => await ReloadFiles());
        public RelayCommand CancelFileSearchCommand => cancelFileSearchCmd ??= new RelayCommand(HandleCancelSearch);

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes);
        public RelayCommand ClearCacheCommand => clearCacheCmd ??= new RelayCommand(async () => await FileStorage.Invalidate());

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
                    detailsTab.ContentVM = GetDetailsVMByFileExtension(x.File);
                }
            }, true);

            Messenger.Default.Register<FileDeletingMessage>(this, x => {
                var idx = LocatedFiles.IndexOf(x.File);
                LocatedFiles.Remove(x.File);
                idx = idx.Clamp(0, LocatedFiles.Count - 1);
                SelectedFile = LocatedFiles[idx];
                try
                {
                    File.Delete(x.File.FilePath);
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

        private static readonly HashSet<string> image_extensions = new HashSet<string>() 
        { 
            "JPG", "JPEG", "TIFF", "GIF", "PNG", "BMP", "EMF", "EXIF", "ICO", "WMF"
        };
        private static bool IsImageExtension(string ext)
        {
            return image_extensions.Contains(ext.ToUpperInvariant());
        }

        private static ViewModelBase GetDetailsVMByFileExtension(GenericFileVM vm)
        {
            var ext = Path.GetExtension(vm.FilePath);
            ext = ext.TrimStart('.');
            if (IsImageExtension(ext))
                return new ImageDetailsVM(vm);
            else
                return new FileDetailsVM(vm);
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

        private async Task<int> RunFilesHashingAsync(IProgress<ProgressArgs> progress, CancellationToken token)
        {
            int processed = 0;
            var total = LocatedFiles.Count;

            var results = await TaskExtensions.ForEachAsync(LocatedFiles, async (x) =>
            {
                if (string.IsNullOrEmpty(x.Hash))
                {
                    var needCacheUpdate = false;

                    var cachedRecord = await FileStorage.GetFileRecordAsync(x.FilePath);
                    if (cachedRecord == null)   //no cache record found
                        cachedRecord = PersistentFileRecord.Create(x.FilePath);

                    var time = PersistentFileRecord.ReadModificationTime(x.FilePath);
                    if (time.HasValue)
                    {
                        if (cachedRecord.Modified == time)   //cached record is valid - file hasn't been changed
                        {
                            if (cachedRecord.TryGetData(HashData.Key, out HashData data))   //success - use cached value
                            {
                                x.Hash = data.Hash;
                                System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(x.FilePath)}: loaded from cache");
                            }
                            else    //no cached Hash
                            {
                                x.Hash = await Utils.GetFileHashAsync(x.FilePath);
                                System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(x.FilePath)}: no cached hash, calculated");
                                needCacheUpdate = true;
                            }
                        }
                        else    //cached value expired
                        {
                            await FileStorage.RemoveFileRecordAsync(x.FilePath);
                            x.Hash = await Utils.GetFileHashAsync(x.FilePath);
                            System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(x.FilePath)}: file modified, hash calculated");
                            needCacheUpdate = true;
                        }
                    } // else current file can't be read - skip

                    if (needCacheUpdate)
                    {
                        cachedRecord.SetData(new HashData() { Hash = x.Hash });
                        await FileStorage.UpdateFileRecordAsync(x.FilePath, cachedRecord);
                        System.Diagnostics.Debug.WriteLine($"{Path.GetFileName(x.FilePath)}: cache updated");
                    }
                }

                var proc = Interlocked.Increment(ref processed);
                progress.Report(new ProgressArgs($"Hashed {proc} of {total}", proc * 100.0 / total));
                return true;
            }, 4);

            return results.Count(x => x);
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

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public string Hash { get => hash; set => Set(ref hash, value); }
        public long FileSize { get => fileSize; set => Set(ref fileSize, value); }

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
        public ObservableCollection<GenericFileVM> Files { get; }

        public FileGroupVM(IEnumerable<GenericFileVM> conflictingFiles)
        {
            Files = new ObservableCollection<GenericFileVM>();
            foreach (var item in conflictingFiles)
            {
                Files.Add(item);
            }

            Messenger.Default.Register<FileDeletingMessage>(this, x => {
                Files.Remove(x.File);
            });
        }
    }

    public class ConflictCollectionVM : ViewModelBase
    {
        private RelayCommand previousConflictCommand;
        private RelayCommand nextConflictCommand;
        private FileGroupVM currentConflict;

        public ObservableCollection<FileGroupVM> Conflicts { get; }

        public FileGroupVM CurrentConflict
        {
            get => currentConflict;
            set
            {
                if (Set(ref currentConflict, value))
                {
                    NextConflictCommand.RaiseCanExecuteChanged();
                    PreviousConflictCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(CurrentIndex));
                }
            }
        }
        public int CurrentIndex => Conflicts.IndexOf(CurrentConflict);

        public RelayCommand NextConflictCommand => nextConflictCommand ??= new RelayCommand(HandleNext, CanGoNext);

        private void HandleNext()
        {
            if(CanGoNext())
                CurrentConflict = Conflicts[CurrentIndex + 1];
        }

        private bool CanGoNext()
        {
            return CurrentIndex >= 0 && CurrentIndex < Conflicts.Count - 1;
        }

        public RelayCommand PreviousConflictCommand => previousConflictCommand ??= new RelayCommand(HandlePrevious, CanGoBack);

        private void HandlePrevious()
        {
            if(CanGoBack())
                CurrentConflict = Conflicts[CurrentIndex - 1];
        }

        private bool CanGoBack()
        {
            return CurrentIndex > 0;
        }

        public ConflictCollectionVM()
        {
            Conflicts = new ObservableCollection<FileGroupVM>();
            Conflicts.CollectionChanged += Conflicts_CollectionChanged;

            if (IsInDesignMode)
            {
                Conflicts.Add(new FileGroupVM(new GenericFileVM[] { new GenericFileVM(), new GenericFileVM() }));
                Conflicts.Add(new FileGroupVM(new GenericFileVM[] { new GenericFileVM(), new GenericFileVM() }));
            }
        }

        private void Conflicts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldItem = e.OldItems?.OfType<FileGroupVM>().FirstOrDefault();
            var newItem = e.NewItems?.OfType<FileGroupVM>().FirstOrDefault();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (CurrentConflict == null)
                    {
                        CurrentConflict = Conflicts.First();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    if (oldItem == CurrentConflict)
                    {
                        CurrentConflict = newItem;
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    CurrentConflict = null;
                    break;
                default:
                    break;
            }
        }
    }
}
