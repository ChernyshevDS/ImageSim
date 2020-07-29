using GalaSoft.MvvmLight.Messaging;

namespace ImageSim.Messages
{
    public enum FileOperation { Exclude, Delete };

    public class FileOperationMessage : MessageBase
    {
        public FileOperationMessage(string path, FileOperation reason)
        {
            FilePath = path;
            Action = reason;
        }

        public FileOperation Action { get; }
        public string FilePath { get; }
    }
}
