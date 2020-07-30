using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageSim.Messages
{
    public class CurrentFileChangedMessage : MessageBase
    {
        public CurrentFileChangedMessage(string file)
        {
            File = file;
        }

        public string File { get; }
    }
}
