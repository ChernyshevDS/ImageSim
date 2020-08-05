using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class ConflictCollectionClearedMessage : MessageBase
    {
        public ConflictCollectionClearedMessage(ViewModelBase vm)
        {
            ConflictsVM = vm;
        }

        public ViewModelBase ConflictsVM { get; }
    }
}
