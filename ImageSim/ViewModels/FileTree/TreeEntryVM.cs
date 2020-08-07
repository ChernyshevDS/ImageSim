using GalaSoft.MvvmLight;
using ImageSim.Collections;

namespace ImageSim.ViewModels.FileTree
{
    public class TreeEntryVM : ViewModelBase
    {
        private SortedObservableCollection<TreeEntryVM> children;
        private string fullPath;
        private string name;
        private bool isExpanded;
        private bool isSelected;

        public SortedObservableCollection<TreeEntryVM> Children { get => children; set => Set(ref children, value); }
        public string FullPath { get => fullPath; set => Set(ref fullPath, value); }
        public string Name { get => name; set => Set(ref name, value); }
        public bool IsExpanded { get => isExpanded; set => Set(ref isExpanded, value); }
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (Set(ref isSelected, value))
                    OnSelectionChanged();
            }
        }
        public bool IsFolder { get; protected set; } = false;

        protected virtual void OnSelectionChanged() { }

        public override string ToString() => Name;
    }
}
