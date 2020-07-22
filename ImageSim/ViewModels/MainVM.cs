﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
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
                {
                    Messenger.Default.Send(new CurrentFileChangedMessage(value));
                }
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

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    LocatedFiles.Add(new ImageFileVM() { FilePath = "File " + (i + 1), Hash = "ABCDEF" });
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
                    LocatedFiles.Add(new ImageFileVM() { FilePath = item });
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
            int processed = 0;
            var total = LocatedFiles.Count;
            Parallel.ForEach(LocatedFiles, (x, state) => {
                if (string.IsNullOrEmpty(x.Hash))
                {
                    x.Hash = Utils.GetFileHash(x.FilePath);
                }

                var proc = Interlocked.Increment(ref processed);
                System.Diagnostics.Debug.WriteLine("Hashed {0} of {1}", proc, total);
            });

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

    public class FileDeletingMessage : MessageBase
    {
        public FileDeletingMessage(ImageFileVM vm)
        {
            File = vm;
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
                if (x.File == null)
                {
                    FilePath = string.Empty;
                    Hash = string.Empty;
                    Width = 0;
                    Height = 0;
                    return;
                }

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

            Messenger.Default.Register<FileDeletingMessage>(this, x => {
                Files.Remove(x.File);
            });
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
        private RelayCommand previousConflictCommand;
        private RelayCommand nextConflictCommand;
        private ImageGroupVM currentConflict;

        public ObservableCollection<ImageGroupVM> Conflicts { get; }

        public ImageGroupVM CurrentConflict
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
            CurrentConflict = Conflicts[CurrentIndex + 1];
        }

        private bool CanGoNext()
        {
            return CurrentIndex >= 0 && CurrentIndex < Conflicts.Count - 1;
        }

        public RelayCommand PreviousConflictCommand => previousConflictCommand ??= new RelayCommand(HandlePrevious, CanGoBack);

        private void HandlePrevious()
        {
            CurrentConflict = Conflicts[CurrentIndex - 1];
        }

        private bool CanGoBack()
        {
            return CurrentIndex > 0;
        }

        public ConflictCollectionVM()
        {
            Conflicts = new ObservableCollection<ImageGroupVM>();
            Conflicts.CollectionChanged += Conflicts_CollectionChanged;

            if (IsInDesignMode)
            {
                Conflicts.Add(new ImageGroupVM(new ImageFileVM[] { new ImageFileVM(), new ImageFileVM() }));
                Conflicts.Add(new ImageGroupVM(new ImageFileVM[] { new ImageFileVM(), new ImageFileVM() }));
            }
        }

        private void Conflicts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldItem = e.OldItems?.OfType<ImageGroupVM>().FirstOrDefault();
            var newItem = e.NewItems?.OfType<ImageGroupVM>().FirstOrDefault();

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

    public class ImageFileVM : ObservableObject
    {
        private RelayCommand deleteCommand;
        private string filePath;
        private string hash;

        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public string Hash { get => hash; set => Set(ref hash, value); }

        public RelayCommand DeleteCommand => deleteCommand ??= new RelayCommand(HandleDelete);

        private void HandleDelete()
        {
            Messenger.Default.Send(new FileDeletingMessage(this));
        }
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

        public static int Clamp(this int val, int min, int max)
        {
            return Math.Max(min, Math.Min(val, max));
        }
    }
}
