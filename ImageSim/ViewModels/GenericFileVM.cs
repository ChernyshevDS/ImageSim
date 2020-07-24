using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;

namespace ImageSim.ViewModels
{
    public class GenericFileVM : ObservableObject
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
}
