using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System;

namespace ImageSim.ViewModels
{
    public class HashConflictEntryVM : ViewModelBase
    {
        private readonly HashConflictVM ParentVM;
        private string filePath;

        private RelayCommand deleteCommand;
        private RelayCommand keepCommand;

        public RelayCommand DeleteCommand => deleteCommand ??= new RelayCommand(HandleDelete);
        public RelayCommand KeepCommand => keepCommand ??= new RelayCommand(HandleKeep);

        public string FilePath { get => filePath; set => Set(ref filePath, value); }

        public HashConflictEntryVM(HashConflictVM parent)
        {
            ParentVM = parent;
        }

        private void HandleDelete()
        {
            Messenger.Default.Send(new FileOperationMessage(FilePath, FileOperation.Delete));
        }

        private void HandleKeep()
        {
            ParentVM.KeepExclusive(this);
        }
    }
}
