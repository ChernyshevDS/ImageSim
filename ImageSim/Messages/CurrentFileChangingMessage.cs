using GalaSoft.MvvmLight.Messaging;

namespace ImageSim.Messages
{
    public class SetCurrentFileMessage : MessageBase
    {
        public SetCurrentFileMessage(string file)
        {
            File = file;
        }

        public string File { get; }
    }
}
