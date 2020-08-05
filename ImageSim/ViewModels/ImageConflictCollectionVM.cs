using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System.Collections.Generic;

namespace ImageSim.ViewModels
{
    public class ImageConflictCollectionVM : ConflictCollectionVM<ConflictDescriptor>
    {
        public ImageConflictCollectionVM() : this(null) { }

        public ImageConflictCollectionVM(IList<ConflictDescriptor> source) : base(source)
        {
            Messenger.Default.Register<FileRemovedMessage>(this, msg =>
            {
                RemoveAll(x => msg.Path == x.Left || msg.Path == x.Right);
            });

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
            return new ImageDCTConflictVM(descr.Left, descr.Right) 
            { 
                SimilarityMetric = descr.Similarity
            };
        }
    }
}
