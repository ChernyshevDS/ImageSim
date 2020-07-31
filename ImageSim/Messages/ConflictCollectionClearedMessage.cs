using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class ConflictCollectionClearedMessage : MessageBase
    {
        public ConflictCollectionClearedMessage(ConflictCollectionVM vm)
        {
            ConflictsVM = vm;
        }

        public ConflictCollectionVM ConflictsVM { get; }
    }
}
