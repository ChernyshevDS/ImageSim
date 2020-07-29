using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class ConflictResolvedMessage : MessageBase
    {
        public ConflictResolvedMessage(ConflictVM vm)
        {
            Conflict = vm;
        }

        public ConflictVM Conflict { get; }
    }
}
