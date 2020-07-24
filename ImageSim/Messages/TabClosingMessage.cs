using GalaSoft.MvvmLight.Messaging;
using ImageSim.ViewModels;

namespace ImageSim.Messages
{
    public class TabClosingMessage : MessageBase
    {
        public TabVM ClosingTab { get; }

        public TabClosingMessage(TabVM closingTab)
        {
            ClosingTab = closingTab;
        }
    }
}
