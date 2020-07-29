using GalaSoft.MvvmLight.Messaging;

namespace ImageSim.Messages
{
    public class FileRemovedMessage : MessageBase
    {
        public FileRemovedMessage(string path) 
        {
            Path = path;
        }

        public string Path { get; }
    }
}
