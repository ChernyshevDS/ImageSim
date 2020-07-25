using GalaSoft.MvvmLight.Messaging;

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
