using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;

namespace ImageSim.ViewModels.FileTree
{
    public class FileTreeEntryVM : TreeEntryVM
    {
        public FileTreeEntryVM()
        {
            Children = null;
        }

        protected override void OnSelectionChanged()
        {
            if (IsSelected)
            {
                Messenger.Default.Send(new SetCurrentFileMessage(this.FullPath));
            }
        }
    }
}
