using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ImageSim.Collections
{
    public class SortedObservableCollection<T> : ICollection<T>, ICollection, IReadOnlyCollection<T>, IReadOnlyList<T>, IList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly List<T> list;
        private readonly IComparer<T> comparer;

        public int Count => list.Count;
        public bool IsReadOnly => ((ICollection<T>)list).IsReadOnly;

        #region ICollection<T> (partly)
        public bool Contains(T item) => this.IndexOf(item) >= 0;
        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
        #endregion

        #region ICollection
        bool ICollection.IsSynchronized => ((ICollection)list).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)list).SyncRoot;
        void ICollection.CopyTo(Array array, int index) => ((ICollection)list).CopyTo(array, index);
        #endregion

        #region IEnumerable
        public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)list).GetEnumerator();
        #endregion

        #region IReadOnlyList
        public T this[int index] => list[index];
        #endregion

        #region INotify_Changed
        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
        #endregion

        #region IList
        void IList<T>.Insert(int index, T item) => throw new InvalidOperationException();
        T IList<T>.this[int index] { get => list[index]; set => throw new InvalidOperationException(); }

        bool IList.IsFixedSize => false;
        object IList.this[int index] { get => list[index]; set => throw new InvalidOperationException(); }

        int IList.Add(object value)
        {
            if (!(value is T val))
                throw new ArgumentException();

            this.Add(val);
            return this.IndexOf(val);
        }

        bool IList.Contains(object value)
        {
            if (!(value is T val))
                return false;
            return this.Contains(val);
        }

        int IList.IndexOf(object value)
        {
            if (!(value is T val))
                return -1;
            return this.IndexOf(val);
        }

        void IList.Insert(int index, object value)
        {
            throw new InvalidOperationException();
        }

        void IList.Remove(object value)
        {
            if (!(value is T val))
                return;
            this.Remove(val);
        }
        #endregion

        public SortedObservableCollection() : this(null, null) { }
        public SortedObservableCollection(IComparer<T> comparer) : this(null, comparer) { }
        public SortedObservableCollection(IEnumerable<T> source) : this(source, null) { }

        public SortedObservableCollection(IEnumerable<T> source, IComparer<T> comparer)
        {
            this.comparer = comparer ?? Comparer<T>.Default;
            if (source != null)
            {
                list = source.ToList();
                list.Sort(comparer);
                RaiseCountChanged();
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            else
            {
                list = new List<T>();
            }
        }

        public int IndexOf(T item)
        {
            var index = list.BinarySearch(item, comparer);
            return index < 0 ? -1 : index;
        }

        public void Add(T item)
        {
            var pos = GetInsertPosition(item);
            list.Insert(pos, item);
            RaiseCountChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, pos));
        }

        public void Clear()
        {
            list.Clear();
            RaiseCountChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Remove(T item)
        {
            var pos = list.BinarySearch(item, comparer);
            if (pos < 0)
                return false;
            list.RemoveAt(pos);
            RaiseCountChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, pos));
            return true;
        }

        public void RemoveAt(int index)
        {
            var item = list[index];
            list.RemoveAt(index);
            RaiseCountChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        private void RaiseCountChanged() => OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));

        private int GetInsertPosition(T newItem)
        {
            var index = list.BinarySearch(newItem, comparer);
            if (index < 0)
                index = ~index;
            return index;
        }
    }
}
