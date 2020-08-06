using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using ImageSim.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ImageSim.ViewModels
{
    public class FileListVM : ViewModelBase
    {
        private readonly IFileService FileService;
        private readonly ObservableCollection<string> Files = new ObservableCollection<string>();
        private readonly HashSet<string> FileSet = new HashSet<string>();
        
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
                Files.Add("C:\\Workspace\\Images\\1.jpg");
                Files.Add("C:\\Workspace\\Images\\2.jpg");
                Files.Add("C:\\Workspace\\Images\\3.jpg");
                Files.Add("C:\\Workspace\\smth.txt");
                Files.Add("C:\\Yolo\\yolo.txt");
                Files.Add("D:\\blabla.txt");
                return;
            }

            Files.CollectionChanged += Files_CollectionChanged;

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

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var newItem = e.NewItems?.Cast<string>().FirstOrDefault();
            var oldItem = e.OldItems?.Cast<string>().FirstOrDefault();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    FileSet.Add(newItem);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    FileSet.Remove(oldItem);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    FileSet.Remove(oldItem);
                    FileSet.Add(newItem);
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    FileSet.Clear();
                    break;
                default:
                    break;
            }
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

        public async Task<bool> AddFileAsync(string path)
        {
            var canAdd = await Task.Run(() => !FileSet.Contains(path));
            if (canAdd)
                Files.Add(path);
            return canAdd;
        }

        public void Clear()
        {
            this.Files.Clear();
            Messenger.Default.Send(new FileListResetMessage());
        }
    }

    public class FileTreeVM : ViewModelBase
    {
        private readonly FileListVM _source;
        private readonly TreeEntryVM _root;

        private RelayCommand zipCommand;
        private RelayCommand unzipCommand;

        public ObservableCollection<TreeEntryVM> Entries { get; }

        public RelayCommand ZipCommand => zipCommand ??= new RelayCommand(HandleZip);

        private void HandleZip()
        {
            foreach (var item in _root.Children.OfType<FolderTreeEntryVM>())
            {
                item.Zip();
            }
        }

        public RelayCommand UnzipCommand => unzipCommand ??= new RelayCommand(HandleUnzip);

        private void HandleUnzip()
        {
            foreach (var item in _root.Children.OfType<FolderTreeEntryVM>())
            {
                item.Unzip();
            }
        }

        public FileTreeVM(FileListVM source)
        {
            this._source = source;
            _root = new TreeEntryVM()
            {
                Children = new ObservableCollection<TreeEntryVM>()
            };
            Entries = _root.Children;

            BuildTree(source.LocatedFiles);
            CollectionChangedEventManager.AddHandler(source.LocatedFiles, FileTreeVM_CollectionChanged);
        }

        private void FileTreeVM_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var newItem = e.NewItems?.OfType<string>().FirstOrDefault();
            var oldItem = e.OldItems?.OfType<string>().FirstOrDefault();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertFile(newItem);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveEntry(oldItem);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    break;
                case NotifyCollectionChangedAction.Move:
                    //nop
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _root.Children.Clear();
                    break;
                default:
                    break;
            }
        }

        private void BuildTree(IEnumerable<string> paths)
        {
            foreach (var item in paths)
            {
                InsertFile(item);
            }
        }

        public void InsertFile(string path)
        {
            var parts = SplitPath(path);
            var currentEntry = _root;
            var currentPath = string.Empty;
            foreach (var name in parts.Take(parts.Length - 1))
            {
                currentPath = JoinPath(currentPath, name);

                var folder = currentEntry.Children.OfType<FolderTreeEntryVM>()
                    .FirstOrDefault(x => x.Name == name);

                if (folder == null)
                {
                    var fentry = CreateFolderEntry(currentPath);
                    currentEntry.Children.Add(fentry);
                    currentEntry = fentry;
                }
                else
                {
                    currentEntry = folder;
                }
            }
            var fileEntry = CreateFileEntry(path);
            currentEntry.Children.Add(fileEntry);
        }

        public bool RemoveEntry(string path)
        {
            var currentEntry = _root;
            var parts = SplitPath(path);
            var entryStack = new Stack<TreeEntryVM>(parts.Length);
            entryStack.Push(_root);
            foreach (var item in parts)
            {
                var entry = currentEntry.Children.FirstOrDefault(x => x.Name == item);
                if (entry == null)
                {
                    return false;
                }
                else
                {
                    entryStack.Push(entry);
                    currentEntry = entry;
                }
            }

            var toRemove = entryStack.Pop();
            var parent = entryStack.Pop();
            while (parent.Children.Count == 1)
            {
                toRemove = parent;
                parent = entryStack.Pop();
                if (entryStack.Count == 0)
                    break;
            }
            parent.Children.Remove(toRemove);
            return true;
        }

        private static readonly char[] _path_sep = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private static string[] SplitPath(string path)
            => path.Split(_path_sep, System.StringSplitOptions.RemoveEmptyEntries);

        private static string JoinPath(params string[] entries)
        {
            var items = entries.ToArray();
            if (items[0].EndsWith(':'))
                items[0] += Path.DirectorySeparatorChar;
            return Path.Combine(items);
        }

        private static FileTreeEntryVM CreateFileEntry(string path)
            => new FileTreeEntryVM() { FullPath = path, IsExpanded = false, Name = Path.GetFileName(path) };

        private static FolderTreeEntryVM CreateFolderEntry(string path)
            => new FolderTreeEntryVM() { FullPath = path, IsExpanded = false, Name = SplitPath(path).Last() };
    }

    public class TreeEntryVM : ViewModelBase
    {
        private ObservableCollection<TreeEntryVM> children;
        private string fullPath;
        private string name;
        private bool isExpanded;

        public ObservableCollection<TreeEntryVM> Children { get => children; set => Set(ref children, value); }
        public string FullPath { get => fullPath; set => Set(ref fullPath, value); }
        public string Name { get => name; set => Set(ref name, value); }
        public bool IsExpanded { get => isExpanded; set => Set(ref isExpanded, value); }

        public override string ToString() => Name;
    }

    public class FileTreeEntryVM : TreeEntryVM
    {
        public FileTreeEntryVM()
        {
            Children = null;
        }
    }

    public class FolderTreeEntryVM : TreeEntryVM
    {
        private bool isZipped;
        private ObservableCollection<TreeEntryVM> visibleChildren;
        private string visibleName;

        public bool IsZipped { get => isZipped; protected set => Set(ref isZipped, value); }
        public string VisibleName { get => visibleName; protected set => Set(ref visibleName, value); }
        public ObservableCollection<TreeEntryVM> VisibleChildren { get => visibleChildren; protected set => Set(ref visibleChildren, value); }

        public FolderTreeEntryVM()
        {
            Children = new ObservableCollection<TreeEntryVM>();
        }

        public void Zip()
        {
            if (Children.Count == 1 && Children[0] is FolderTreeEntryVM childFolder)
            {
                childFolder.Zip();
                
                VisibleChildren = childFolder.VisibleChildren;
                VisibleName = Path.Combine(Name, childFolder.VisibleName);
                
                //return true;
            }
            else 
            {
                foreach (var item in Children.OfType<FolderTreeEntryVM>())
                {
                    item.Zip();
                }

                VisibleChildren = Children;
                VisibleName = Name;
                //return false;
            }
        }

        public void Unzip()
        {
            VisibleName = Name;
            VisibleChildren = Children;
            foreach (var item in Children.OfType<FolderTreeEntryVM>())
            {
                item.Unzip();
            }
        }
    }
}
