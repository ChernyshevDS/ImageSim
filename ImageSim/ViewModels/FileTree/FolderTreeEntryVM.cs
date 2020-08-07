using ImageSim.Collections;
using System.IO;
using System.Linq;

namespace ImageSim.ViewModels.FileTree
{
    public class FolderTreeEntryVM : TreeEntryVM
    {
        private bool isZipped;
        private SortedObservableCollection<TreeEntryVM> visibleChildren;
        private string visibleName;

        public bool IsZipped { get => isZipped; protected set => Set(ref isZipped, value); }
        public string VisibleName { get => visibleName; protected set => Set(ref visibleName, value); }
        public SortedObservableCollection<TreeEntryVM> VisibleChildren { get => visibleChildren; protected set => Set(ref visibleChildren, value); }

        public FolderTreeEntryVM()
        {
            Children = new SortedObservableCollection<TreeEntryVM>(new TreeEntryComparer());
            IsFolder = true;
        }

        public void Zip()
        {
            if (Children.Count == 1 && Children[0] is FolderTreeEntryVM childFolder)
            {
                childFolder.Zip();

                VisibleChildren = childFolder.VisibleChildren;
                VisibleName = Path.Combine(Name, childFolder.VisibleName);
                IsZipped = true;
            }
            else
            {
                foreach (var item in Children.OfType<FolderTreeEntryVM>())
                {
                    item.Zip();
                }

                VisibleChildren = Children;
                VisibleName = Name;
                IsZipped = false;
            }
        }

        public void Unzip()
        {
            VisibleName = Name;
            VisibleChildren = Children;
            IsZipped = false;
            foreach (var item in Children.OfType<FolderTreeEntryVM>())
            {
                item.Unzip();
            }
        }
    }
}
