using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;

namespace ImageSim.ViewModels
{
    public class ConflictVM : ViewModelBase
    {
        private RelayCommand markResolvedCmd;
        private bool isLastConflict;

        public RelayCommand ResolveCommand => markResolvedCmd ??= new RelayCommand(MarkAsResolved);

        public bool IsLastConflict { get => isLastConflict; set => Set(ref isLastConflict, value); }

        public void MarkAsResolved()
        {
            Messenger.Default.Send(new ConflictResolvedMessage(this));
        }
    }
}
