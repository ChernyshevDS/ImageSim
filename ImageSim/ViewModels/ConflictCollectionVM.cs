using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ImageSim.ViewModels
{
    public class ConflictCollectionVM : ViewModelBase
    {
        private RelayCommand previousConflictCommand;
        private RelayCommand nextConflictCommand;
        private ConflictVM currentConflict;

        public ObservableCollection<ConflictVM> Conflicts { get; }

        public ConflictVM CurrentConflict
        {
            get => currentConflict;
            set
            {
                if (Set(ref currentConflict, value))
                {
                    NextConflictCommand.RaiseCanExecuteChanged();
                    PreviousConflictCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(CurrentIndex));
                }
            }
        }
        public int CurrentIndex => Conflicts.IndexOf(CurrentConflict);

        public RelayCommand NextConflictCommand => nextConflictCommand ??= new RelayCommand(HandleNext, CanGoNext);

        private void HandleNext()
        {
            if(CanGoNext())
                CurrentConflict = Conflicts[CurrentIndex + 1];
        }

        private bool CanGoNext()
        {
            return CurrentIndex >= 0 && CurrentIndex < Conflicts.Count - 1;
        }

        public RelayCommand PreviousConflictCommand => previousConflictCommand ??= new RelayCommand(HandlePrevious, CanGoBack);

        private void HandlePrevious()
        {
            if(CanGoBack())
                CurrentConflict = Conflicts[CurrentIndex - 1];
        }

        private bool CanGoBack()
        {
            return CurrentIndex > 0;
        }

        public ConflictCollectionVM()
        {
            Conflicts = new ObservableCollection<ConflictVM>();

            if (IsInDesignMode)
            {
                var conflict = HashConflictVM.FromPaths(new string[] { "File 1", "File 2" });
                Conflicts.Add(conflict);
                return;
            }

            Conflicts.CollectionChanged += Conflicts_CollectionChanged;

            Messenger.Default.Register<ConflictResolvedMessage>(this, msg => {
                Conflicts.Remove(msg.Conflict);

                if (Conflicts.Count == 0)
                {
                    Messenger.Default.Send(new ConflictCollectionClearedMessage(this));
                }
            });
        }

        private void Conflicts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldItem = e.OldItems?.OfType<ConflictVM>().FirstOrDefault();
            var newItem = e.NewItems?.OfType<ConflictVM>().FirstOrDefault();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (CurrentConflict == null)
                    {
                        CurrentConflict = Conflicts.FirstOrDefault();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    if (oldItem == CurrentConflict)
                    {
                        CurrentConflict = newItem ?? Conflicts.FirstOrDefault();
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    CurrentConflict = null;
                    break;
                default:
                    break;
            }

            var last = Conflicts.LastOrDefault();
            foreach (var item in Conflicts)
            {
                item.IsLastConflict = (item == last);
            }
        }
    }
}
