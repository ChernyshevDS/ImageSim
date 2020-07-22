using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        private RelayCommand setWorkingFolderCmd;
        private RelayCommand reloadFilesCmd;
        private RelayCommand cancelFileSearchCmd;
        private RelayCommand compareHashesCommand;
        private bool isFileSearchInProgress = false;
        private CancellationTokenSource cancellationSource;
        private ImageFileVM selectedFile;

        public RelayCommand SetWorkingFolderCommand => setWorkingFolderCmd ??= new RelayCommand(HandleSetWorkingFolder);
        public RelayCommand ReloadFilesCommand => reloadFilesCmd ??= new RelayCommand(async () => await ReloadFiles());
        public RelayCommand CancelFileSearchCommand => cancelFileSearchCmd ??= new RelayCommand(HandleCancelSearch);

        public RelayCommand CompareHashesCommand => compareHashesCommand ??= new RelayCommand(HandleCompareHashes);

        public bool IsFileSearchInProgress { get => isFileSearchInProgress; set => Set(ref isFileSearchInProgress, value); }
        public ObservableCollection<ImageFileVM> LocatedFiles { get; }
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
        public ImageFileVM SelectedFile
        {
            get => selectedFile;
            set
            {
                if (Set(ref selectedFile, value))
                    Messenger.Default.Send(new CurrentFileChangedMessage(value));
            }
        }

        public MainVM(IImageProvider imageProvider)
        {
            ImageProvider = imageProvider;
            LocatedFiles = new ObservableCollection<ImageFileVM>();


            Tabs = new ObservableCollection<TabVM>()
            {
                new TabVM() { Header = "Current file", ContentVM = new ImageDetailsVM() }
            };

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    LocatedFiles.Add(new ImageFileVM() { FilePath = "File " + (i + 1), Hash = "ABCDEF" });
                }

                Tabs.Add(new TabVM() { Header = "Conflicts", ContentVM = new ConflictCollectionVM() });
            }
        }

        private async void HandleSetWorkingFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select working folder",
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WorkingFolder = dlg.SelectedPath;
                await ReloadFiles();
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
                    LocatedFiles.Add(new ImageFileVM() { FilePath = item, Hash = Utils.GetFileHash(item) });
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
            var groups = LocatedFiles.GroupBy(x => x.Hash).Where(x => x.Count() > 1);
            var coll = new ConflictCollectionVM();
            foreach (var group in groups)
            {
                var conflict = new ImageGroupVM(group);
                coll.Conflicts.Add(conflict);
            }
            this.Tabs.Add(new TabVM() { Header = "Hash conflicts", ContentVM = coll });
        }
    }

    public class CurrentFileChangedMessage : MessageBase
    {
        public CurrentFileChangedMessage(ImageFileVM file)
        {
            File = file;
        }

        public ImageFileVM File { get; }
    }

    public class ImageDetailsVM : ViewModelBase
    {
        private string filePath;
        private string hash;
        private int width;
        private int height;

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public string Hash { get => hash; set => Set(ref hash, value); }
        public int Width { get => width; set => Set(ref width, value); }
        public int Height { get => height; set => Set(ref height, value); }

        public ImageDetailsVM()
        {
            Messenger.Default.Register<CurrentFileChangedMessage>(this, (x) =>
            {
                FilePath = x.File.FilePath;
                Hash = x.File.Hash;
                using var img = System.Drawing.Image.FromFile(FilePath);
                Width = img.Width;
                Height = img.Height;
            });
        }
    }

    public class ImageGroupVM : ViewModelBase
    { 
        public ObservableCollection<ImageFileVM> Files { get; }

        public ImageGroupVM(IEnumerable<ImageFileVM> conflictingFiles)
        {
            Files = new ObservableCollection<ImageFileVM>();
            foreach (var item in conflictingFiles)
            {
                Files.Add(item);
            }
        }
    }

    public class TabVM : ViewModelBase
    {
        private string header;
        private object contentVM;

        public string Header { get => header; set => Set(ref header, value); }
        public object ContentVM { get => contentVM; set => Set(ref contentVM, value); }
    }

    public class ConflictCollectionVM : ViewModelBase
    {
        public ObservableCollection<ImageGroupVM> Conflicts { get; }
        
        public ConflictCollectionVM()
        {
            Conflicts = new ObservableCollection<ImageGroupVM>();

            if (IsInDesignMode)
            {
                Conflicts.Add(new ImageGroupVM(new ImageFileVM[] { new ImageFileVM(), new ImageFileVM() }));
                Conflicts.Add(new ImageGroupVM(new ImageFileVM[] { new ImageFileVM(), new ImageFileVM() }));
            }
        }
    }

    public class ImageFileVM : ObservableObject
    {
        private string filePath;
        private string hash;

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public string Hash { get => hash; set => Set(ref hash, value); }
    }

    public static class Utils
    {
        public static string GetFileHash(string path)
        {
            using var fs = System.IO.File.OpenRead(path);
            var alg = System.Security.Cryptography.MD5.Create();
            var hash = alg.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
        }
    }
}
