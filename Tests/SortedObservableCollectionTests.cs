using ImageSim.ViewModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Tests
{
    public class SortedObservableCollectionTests
    {
        [Test]
        public void Test_Create()
        {
            var coll = new SortedObservableCollection<int>();
            Assert.AreEqual(0, coll.Count);
            Assert.IsFalse(coll.IsReadOnly);
        }

        [Test]
        public void Test_CreateComparer()
        {
            var coll = new SortedObservableCollection<int>(Comparer<int>.Create((x,y) => x - y));
            Assert.AreEqual(0, coll.Count);
            Assert.IsFalse(coll.IsReadOnly);
        }

        [Test]
        public void Test_CreateFromSource()
        {
            var source = Enumerable.Range(0, 6);

            var coll = new SortedObservableCollection<int>(source);
            Assert.AreEqual(6, coll.Count);
            Assert.IsFalse(coll.IsReadOnly);
            CollectionAssert.AreEqual(source, coll);
        }

        [Test]
        public void Test_CreateFull()
        {
            var source = Enumerable.Range(0, 6);

            var coll = new SortedObservableCollection<int>(source, Comparer<int>.Create((x, y) => y - x));
            Assert.AreEqual(6, coll.Count);
            Assert.IsFalse(coll.IsReadOnly);
            CollectionAssert.AreEqual(source.Reverse(), coll);
        }

        private bool ValidateCollectionEvent(NotifyCollectionChangedEventArgs args, NotifyCollectionChangedAction action,
            int newIndex = -1, object newItem = null, int oldIndex = -1, object oldItem = null)
        {
            var isValid = true;
            isValid &= (args.Action == action);
            isValid &= (args.NewStartingIndex == newIndex);
            if (newItem != null)
            {
                isValid &= (args.NewItems.Count == 1);
                isValid &= (args.NewItems[0].Equals(newItem));
            }
            else 
            {
                isValid &= (args.NewItems == null);
            }
            isValid &= args.OldStartingIndex == oldIndex;
            if (oldItem != null)
            {
                isValid &= (args.OldItems.Count == 1);
                isValid &= (args.OldItems[0].Equals(oldItem));
            }
            else
            {
                isValid &= (args.OldItems == null);
            }
            return isValid;
        }

        [Test]
        public void Test_Add()
        {
            var listener = new EventListener();
            var coll = new SortedObservableCollection<int>();

            coll.PropertyChanged += listener.Consume;
            coll.CollectionChanged += listener.Consume;

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x => 
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Add, newIndex: 0, newItem: 3));
            coll.Add(3);
            listener.MakeAssert();

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x =>
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Add, newIndex: 0, newItem: 1));
            coll.Add(1);
            listener.MakeAssert();

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x =>
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Add, newIndex: 2, newItem: 5));
            coll.Add(5);
            listener.MakeAssert();
            
            CollectionAssert.AreEqual(new int[] { 1, 3, 5 }, coll);
        }

        [Test]
        public void Test_Clear()
        {
            var listener = new EventListener();
            var coll = new SortedObservableCollection<int> { 0, 1, 2 };

            coll.PropertyChanged += listener.Consume;
            coll.CollectionChanged += listener.Consume;

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x =>
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Reset));

            coll.Clear();

            listener.MakeAssert();
            Assert.AreEqual(0, coll.Count);
        }

        [Test]
        public void Test_Indexing()
        {
            var coll = new SortedObservableCollection<int> { 3, 1, 2, 5, 4 };
            
            CollectionAssert.AreEqual(Enumerable.Range(1, 5), coll);
            Assert.AreEqual(0, coll.IndexOf(1));
            Assert.AreEqual(2, coll.IndexOf(3));
            Assert.AreEqual(4, coll.IndexOf(5));
            Assert.AreEqual(-1, coll.IndexOf(150));

            Assert.AreEqual(1, coll[0]);
            Assert.AreEqual(2, coll[1]);
            Assert.AreEqual(5, coll[4]);
        }

        [Test]
        public void Test_Contains()
        {
            var coll = new SortedObservableCollection<int> { 3, 1, 2, 5, 4 };

            Assert.IsTrue(coll.Contains(1));
            Assert.IsTrue(coll.Contains(2));
            Assert.IsTrue(coll.Contains(5));
            Assert.IsFalse(coll.Contains(15));
        }

        [Test]
        public void Test_CopyTo()
        {
            var coll = new SortedObservableCollection<int> { 3, 1, 2, 5, 4 };

            var arr = new int[8] { 7, 7, 7, 7, 7, 7, 7, 7 };
            coll.CopyTo(arr, 2);

            CollectionAssert.AreEqual(new int[] { 7, 7, 1, 2, 3, 4, 5, 7 }, arr);
        }

        [Test]
        public void Test_Remove()
        {
            var listener = new EventListener();
            var coll = new SortedObservableCollection<int>() { 1, 2, 3, 4, 5 };

            coll.PropertyChanged += listener.Consume;
            coll.CollectionChanged += listener.Consume;

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x =>
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Remove, oldIndex: 1, oldItem: 2));

            Assert.IsTrue(coll.Remove(2));

            listener.MakeAssert();
            Assert.AreEqual(4, coll.Count);
            CollectionAssert.AreEqual(new int[] { 1, 3, 4, 5 }, coll);

            Assert.IsFalse(coll.Remove(8));

            listener.Expect<PropertyChangedEventArgs>(x => x.PropertyName == "Count");
            listener.Expect<NotifyCollectionChangedEventArgs>(x =>
                ValidateCollectionEvent(x, NotifyCollectionChangedAction.Remove, oldIndex: 2, oldItem: 4));
            
            coll.RemoveAt(2);
            listener.MakeAssert();
            Assert.AreEqual(3, coll.Count);
            CollectionAssert.AreEqual(new int[] { 1, 3, 5 }, coll);
        }

        internal class EventListener
        {
            internal struct EventExpect
            {
                public Type eventType;
                public Delegate validator;
            }

            private readonly List<EventExpect> expects = new List<EventExpect>();
            private readonly List<EventArgs> events = new List<EventArgs>();
            
            public void Consume(object sender, EventArgs e)
            {
                events.Add(e);
            }

            public void Clear() 
            {
                events.Clear();
                expects.Clear();
            }

            public void Expect<T>(Func<T, bool> is_valid) where T : EventArgs
            {
                expects.Add(new EventExpect() { eventType = typeof(T), validator = is_valid });
            }

            public void MakeAssert(bool autoClear = true)
            {
                Assert.AreEqual(expects.Count, events.Count);
                var lst = events.Zip(expects);
                foreach (var (happened, expected) in lst)
                {
                    Assert.IsInstanceOf(expected.eventType, happened);

                    var result = (bool)expected.validator.DynamicInvoke(happened);
                    Assert.IsTrue(result);
                }

                if (autoClear)
                    Clear();
            }
        }
    }
}
