using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class FileDeletingMessage : MessageBase
    {
        public FileDeletingMessage(GenericFileVM vm)
        {
            File = vm;
        }

        public GenericFileVM File { get; }
    }
}
