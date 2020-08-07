using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ImageSim.Collections
{
    public class ReadOnlySortedObservableCollection<T> : ReadOnlyCollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);

        public ReadOnlySortedObservableCollection(SortedObservableCollection<T> list) : base(list)
        {
            ((INotifyCollectionChanged)Items).CollectionChanged += new NotifyCollectionChangedEventHandler(HandleCollectionChanged);
            ((INotifyPropertyChanged)Items).PropertyChanged += new PropertyChangedEventHandler(HandlePropertyChanged);
        }

        private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
        private void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => OnCollectionChanged(e);
    }
}
