using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class FileDeletingMessage : MessageBase
    {
        public FileDeletingMessage(string path)
        {
            FilePath = path;
        }

        public string FilePath { get; }
    }
}
