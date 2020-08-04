using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;

namespace ImageSim.ViewModels
{
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
}
