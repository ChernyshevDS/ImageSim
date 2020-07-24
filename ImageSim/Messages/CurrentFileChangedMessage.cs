using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageSim.Messages
{
    public class CurrentFileChangedMessage : MessageBase
    {
        public CurrentFileChangedMessage(GenericFileVM file)
        {
            File = file;
        }

        public GenericFileVM File { get; }
    }
}
