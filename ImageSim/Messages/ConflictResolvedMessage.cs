using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class ConflictResolvedMessage : MessageBase
    {
        public ConflictResolvedMessage(FileGroupVM vm)
        {
            Conflict = vm;
        }

        public FileGroupVM Conflict { get; }
    }
}
