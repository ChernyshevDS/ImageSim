using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ImageSim.ViewModels
{
    public abstract class ConflictCollectionVM : ViewModelBase
    { 
        public abstract int ConflictsCount { get; }
        public abstract int CurrentIndex { get; set; }
        public abstract ConflictVM CurrentConflict { get; protected set; }
    }

    public abstract class ConflictCollectionVM<TConflict> : ConflictCollectionVM
    {
        private readonly IList<TConflict> conflicts;
        private RelayCommand previousConflictCommand;
        private RelayCommand nextConflictCommand;
        private ConflictVM currentConflict;
        private int currentIndex = -1;

        protected IReadOnlyList<TConflict> Conflicts => (IReadOnlyList<TConflict>)conflicts;

        public RelayCommand PreviousConflictCommand => previousConflictCommand ??= new RelayCommand(HandlePrevious, CanGoBack);
        public RelayCommand NextConflictCommand => nextConflictCommand ??= new RelayCommand(HandleNext, CanGoNext);
        
        public override int ConflictsCount => conflicts.Count;
        public override ConflictVM CurrentConflict { get => currentConflict; protected set => Set(ref currentConflict, value); }
        public override int CurrentIndex
        {
            get => currentIndex;
            set
            {
                if (SetCurrentIndex(value))
                {
                    UpdateCurrentConflict();
                }
            }
        }

        protected ConflictCollectionVM([AllowNull] IList<TConflict> source)
        {
            conflicts = source ?? new List<TConflict>();
            RaisePropertyChanged(nameof(ConflictsCount));
            SetCurrentIndex(0, true);
            UpdateCurrentConflict();
        }

        protected bool SetCurrentIndex(int index, bool forceUpdate = false)
        {
            index = ConflictsCount == 0 ? -1 : index.Clamp(0, ConflictsCount - 1);
            var changed = Set(ref currentIndex, index, nameof(CurrentIndex));
            if (changed || forceUpdate)
            {
                NextConflictCommand.RaiseCanExecuteChanged();
                PreviousConflictCommand.RaiseCanExecuteChanged();
            }
            return changed;
        }

        protected void UpdateCurrentConflict()
        {
            if (CurrentIndex < 0 || CurrentIndex >= ConflictsCount)
            {
                CurrentConflict = null;
            }
            else 
            {
                CurrentConflict = GetConflictVM(CurrentIndex);
                UpdateLastConflictFlag();
            }
        }

        private void UpdateLastConflictFlag()
        {
            if (CurrentConflict == null || ConflictsCount == 0 || CurrentIndex < 0)
                return;
            CurrentConflict.IsLastConflict = (CurrentIndex == ConflictsCount - 1);
        }

        private void HandleNext()
        {
            if (CanGoNext())
                CurrentIndex += 1;
        }

        private bool CanGoNext() => CurrentIndex >= 0 && CurrentIndex < ConflictsCount - 1;

        private void HandlePrevious()
        {
            if (CanGoBack())
                CurrentIndex -= 1;
        }

        private bool CanGoBack() => CurrentIndex > 0;

        protected abstract ConflictVM GetConflictVM(int conflictIndex);

        public virtual void AddConflict(TConflict conflict)
        {
            conflicts.Add(conflict);
            RaisePropertyChanged(nameof(ConflictsCount));
            if (CurrentConflict == null)
            {
                SetCurrentIndex(0);
                UpdateCurrentConflict();
            }
            UpdateLastConflictFlag();
        }

        public void RemoveConflict(TConflict conflict)
        {
            var idx = conflicts.IndexOf(conflict);
            RemoveConflictAt(idx);
        }

        public virtual void RemoveConflictAt(int index)
        {
            if (index < 0 || index > ConflictsCount)
                return;
            conflicts.RemoveAt(index);
            RaisePropertyChanged(nameof(ConflictsCount));
            if (index < CurrentIndex)
            {
                SetCurrentIndex(CurrentIndex - 1);
            }
            else if (index == CurrentIndex)
            {
                SetCurrentIndex(index);
                UpdateCurrentConflict();
            }
            UpdateLastConflictFlag();
        }

        public void RemoveAll(Predicate<TConflict> filter)
        {
            var removedBeforeCurrent = 0;
            var removed = 0;
            var index = conflicts.Count - 1;
            while (true)
            {
                if (index < 0)
                    break;
                var item = conflicts[index];
                if (filter(item))
                {
                    conflicts.RemoveAt(index);
                    if (index < CurrentIndex)
                        removedBeforeCurrent++;
                    removed++;
                }
                index--;
            }
            SetCurrentIndex(CurrentIndex - removedBeforeCurrent, true);
            UpdateCurrentConflict();
            RaisePropertyChanged(nameof(ConflictsCount));
        }
    }
}
