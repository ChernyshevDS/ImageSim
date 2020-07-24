using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;

namespace ImageSim.ViewModels
{
    public class TabVM : ViewModelBase
    {
        private RelayCommand closeTabCommand;
        private string header;
        private object contentVM;
        private bool canCloseTab = true;

        public RelayCommand CloseTabCommand => closeTabCommand ??= new RelayCommand(HandleCloseTab, () => canCloseTab);

        public bool CanCloseTab
        {
            get => canCloseTab;
            set
            {
                if (Set(ref canCloseTab, value))
                    CloseTabCommand.RaiseCanExecuteChanged();
            }
        }
        public string Header { get => header; set => Set(ref header, value); }
        public object ContentVM { get => contentVM; set => Set(ref contentVM, value); }

        private void HandleCloseTab()
        {
            Messenger.Default.Send(new TabClosingMessage(this));
        }
    }
}
