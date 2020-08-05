using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ImageSim.ViewModels
{
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
                vm.ConflictingFiles.Add(new HashConflictEntryVM(vm) { FilePath = item });
            }
            return vm;
        }

        public void KeepExclusive(HashConflictEntryVM entry)
        {
            Messenger.Default.Unregister<FileRemovedMessage>(this);
            ConflictingFiles.CollectionChanged -= ConflictingFiles_CollectionChanged;
            try
            {
                var toDelete = ConflictingFiles.Where(x => x != entry).ToList();
                foreach (var item in toDelete)
                {
                    Messenger.Default.Send(new FileOperationMessage(item.FilePath, FileOperation.Delete));
                }
            }
            finally
            {
                MarkAsResolved();
            }
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
}
