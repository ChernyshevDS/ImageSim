using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Collections;
using ImageSim.Messages;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace ImageSim.ViewModels.FileTree
{
    public class FileTreeVM : ViewModelBase
    {
        private readonly FileListVM _source;
        private readonly TreeEntryVM _root;
        private RelayCommand zipCommand;
        private RelayCommand unzipCommand;
        private TreeEntryVM selectedItem;

        public SortedObservableCollection<TreeEntryVM> Entries { get; }
        public RelayCommand ZipCommand => zipCommand ??= new RelayCommand(Zip);
        public RelayCommand UnzipCommand => unzipCommand ??= new RelayCommand(Unzip);

        public TreeEntryVM SelectedItem { get => selectedItem; set => Set(ref selectedItem, value); }

        private readonly Action delayed_zip;

        public FileTreeVM(FileListVM source)
        {
            this._source = source;
            _root = new TreeEntryVM()
            {
                Children = new SortedObservableCollection<TreeEntryVM>(new TreeEntryComparer())
            };
            Entries = _root.Children;

            BuildTree(source.LocatedFiles);
            CollectionChangedEventManager.AddHandler(source.LocatedFiles, FileTreeVM_CollectionChanged);

            delayed_zip = new Action(Zip).Debounce(200);

            Messenger.Default.Register<CurrentFileChangedMessage>(this, msg =>
            {
                var selectedNow = FindEntry(msg.OldFile);
                if (selectedNow != null)
                    selectedNow.IsSelected = false;

                var entry = FindEntry(msg.NewFile);
                if (entry != null)
                    entry.IsSelected = true;

                SelectedItem = entry;
            });
        }

        public void Zip()
        {
            foreach (var item in _root.Children.OfType<FolderTreeEntryVM>())
            {
                item.Zip();
            }
        }

        public void Unzip()
        {
            foreach (var item in _root.Children.OfType<FolderTreeEntryVM>())
            {
                item.Unzip();
            }
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

            delayed_zip();
        }

        internal void BuildTree(IEnumerable<string> paths)
        {
            foreach (var item in paths)
            {
                InsertFile(item);
            }
        }

        internal TreeEntryVM FindEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var parts = SplitPath(path);
            var currentEntry = _root;
            foreach (var name in parts)
            {
                var entry = currentEntry.Children.FirstOrDefault(x => x.Name == name);
                if (entry == null)
                    return null;
                currentEntry = entry;
            }
            return currentEntry;
        }

        internal void InsertFile(string path)
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

        internal bool RemoveEntry(string path)
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
}
