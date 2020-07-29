using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System;

namespace ImageSim.ViewModels
{
    public class GenericFileVM : ObservableObject
    {
        private string filePath;
        private RelayCommand deleteCmd;
        private RelayCommand excludeCmd;
        
        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        public RelayCommand DeleteCommand => deleteCmd ??= new RelayCommand(HandleDelete);
        public RelayCommand ExcludeCommand => excludeCmd ??= new RelayCommand(HandleExclude);

        private void HandleDelete()
        {
            Messenger.Default.Send(new FileOperationMessage(FilePath, FileOperation.Delete));
        }

        private void HandleExclude()
        {
            Messenger.Default.Send(new FileOperationMessage(FilePath, FileOperation.Exclude));
        }
    }
}
