using GalaSoft.MvvmLight.Messaging;
using System.Collections.Generic;

namespace ImageSim.Messages
{
    public enum FileOperation { Exclude, Delete };

    public class FileOperationMessage : MessageBase
    {
        public FileOperationMessage(string file, FileOperation operation)
        {
            Files = new string[] { file };
            Action = operation;
        }

        public FileOperationMessage(IReadOnlyList<string> files, FileOperation operation)
        {
            Files = files;
            Action = operation;
        }

        public IReadOnlyList<string> Files { get; }
        public FileOperation Action { get; }
    }
}
