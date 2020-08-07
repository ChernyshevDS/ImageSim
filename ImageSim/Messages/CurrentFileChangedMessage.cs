using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageSim.Messages
{
    public class CurrentFileChangedMessage : MessageBase
    {
        public CurrentFileChangedMessage(string oldFile, string newFile)
        {
            OldFile = oldFile;
            NewFile = newFile;
        }

        public string OldFile { get; }
        public string NewFile { get; }
    }
}
