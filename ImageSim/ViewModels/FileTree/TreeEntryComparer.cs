using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ImageSim.ViewModels.FileTree
{
    public class TreeEntryComparer : IComparer<TreeEntryVM>
    {
        public int Compare([AllowNull] TreeEntryVM x, [AllowNull] TreeEntryVM y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null && y != null)
                return -1;
            if (x != null && y == null)
                return 1;

            //non-nulls here, folders first
            if (x.IsFolder && !y.IsFolder)
                return -1;
            if (!x.IsFolder && y.IsFolder)
                return 1;

            return x.Name.CompareTo(y.Name);
        }
    }
}
