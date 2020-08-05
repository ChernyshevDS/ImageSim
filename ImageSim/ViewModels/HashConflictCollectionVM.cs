using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System.Collections.Generic;

namespace ImageSim.ViewModels
{
    public class HashConflictCollectionVM : ConflictCollectionVM<HashConflictDescriptor>
    {
        public HashConflictCollectionVM() : this(null) { }

        public HashConflictCollectionVM(IList<HashConflictDescriptor> source) : base(source)
        {
            Messenger.Default.Register<ConflictResolvedMessage>(this, msg =>
            {
                if (msg.Conflict == this.CurrentConflict)
                {
                    RemoveConflictAt(CurrentIndex);
                }

                if (Conflicts.Count == 0)
                {
                    Messenger.Default.Send(new ConflictCollectionClearedMessage(this));
                }
            });
        }

        protected override ConflictVM GetConflictVM(int conflictIndex)
        {
            var descr = Conflicts[conflictIndex];
            return HashConflictVM.FromPaths(descr.Paths);
        }
    }
}
