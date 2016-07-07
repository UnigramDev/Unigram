using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Data;

namespace Unigram.Common
{
    public class ListCollectionView : ICollectionView, INotifyPropertyChanged
    {
        #region ** fields

        private object _source;                                 // original data source
        private IList _sourceList;                              // original data source as list
        private Type _itemType;                                 // type of item in the source collection
        private INotifyCollectionChanged _sourceNcc;            // listen to changes in the source
        private List<object> _view;                             // filtered/sorted data source
        private int _index;                                     // cursor position
        private int _updating;                                  // suspend notifications

        private ObservableVector<object> _groups;

        #endregion

        #region ** ctor

        public ListCollectionView(object source)
        {
            // view exposed to consumers
            _view = new List<object>();

            _groups = new ObservableVector<object>();

            // hook up to data source
            Source = source;
        }

        public ListCollectionView()
            : this(null)
        {
        }

        #endregion

        #region ** object model

        /// <summary>
        /// Gets or sets the collection from which to create the view.
        /// </summary>
        public object Source
        {
            get
            {
                return _source;
            }

            set
            {
                if (_source != value)
                {
                    // save new source
                    _source = value;

                    // save new source as list (so we can add/remove etc)
                    _sourceList = value as IList;

                    // get the type of object in the collection
                    _itemType = GetItemType();

                    // listen to changes in the source
                    if (_sourceNcc != null)
                    {
                        _sourceNcc.CollectionChanged -= Source_CollectionChanged;
                    }

                    _sourceNcc = _source as INotifyCollectionChanged;
                    if (_sourceNcc != null)
                    {
                        _sourceNcc.CollectionChanged += Source_CollectionChanged;
                    }

                    // refresh our view
                    HandleSourceChanged();

                    // inform listeners
                    OnPropertyChanged("Source");
                    OnPropertyChanged("HasMoreItems");
                }
            }
        }

        /// <summary>
        /// Update the view from the current source, using the current filter and sort settings.
        /// </summary>
        public void Refresh()
        {
            HandleSourceChanged();
        }

        /// <summary>
        /// Raises the <see cref="CurrentChanging"/> event.
        /// </summary>
        protected virtual void OnCurrentChanging(CurrentChangingEventArgs e)
        {
            if (_updating <= 0)
            {
                CurrentChanging?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="CurrentChanged"/> event.
        /// </summary>
        protected virtual void OnCurrentChanged(object e)
        {
            if (_updating <= 0)
            {
                CurrentChanged?.Invoke(this, e);
                OnPropertyChanged("CurrentItem");
            }
        }

        /// <summary>
        /// Raises the <see cref="VectorChanged"/> event.
        /// </summary>
        public virtual void RaiseVectorChanged(IVectorChangedEventArgs e)
        {
            if (_updating <= 0)
            {
                VectorChanged?.Invoke(this, e);
                OnPropertyChanged("Count");
            }
        }

        #endregion

        #region ** event handlers

        // the original source has changed, update our source list
        private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updating <= 0)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems.Count == 1)
                        {
                            HandleItemAdded(e.NewStartingIndex, e.NewItems[0]);
                        }
                        else
                        {
                            HandleSourceChanged();
                        }

                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems.Count == 1)
                        {
                            HandleItemRemoved(e.OldStartingIndex, e.OldItems[0]);
                        }
                        else
                        {
                            HandleSourceChanged();
                        }

                        break;
                    case NotifyCollectionChangedAction.Move:
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Reset:
                        HandleSourceChanged();
                        break;
                    default:
                        throw new Exception("Unrecognized collection change notification: " + e.Action.ToString());
                }
            }
        }

        #endregion

        #region ** implementation

        // add item to view
        private void HandleItemAdded(int index, object item)
        {
            // add item to view
            var group = new ListCollectionViewGroup(item);
            _groups.Insert(index, group);

            // keep selection on the same item
            if (index <= _index)
            {
                _index++;
            }

            // notify listeners
            RaiseVectorChanged(VectorChangedEventArgs.Reset);
            //_groups.RaiseVectorChanged(new VectorChangedEventArgs(CollectionChange.ItemInserted, index));
        }

        // remove item from view
        private void HandleItemRemoved(int index, object item)
        {
            // compute index into view
            if (index < 0 || index >= _view.Count || !object.Equals(_view[index], item))
            {
                index = _view.IndexOf(item);
            }

            if (index < 0)
            {
                return;
            }

            // remove item from view
            _view.RemoveAt(index);

            // keep selection on the same item
            if (index <= _index)
            {
                _index--;
            }

            // notify listeners
            var e = new VectorChangedEventArgs(CollectionChange.ItemRemoved, index);
            RaiseVectorChanged(e);
        }

        // update view after changes other than add/remove an item
        private void HandleSourceChanged()
        {
            // keep selection if possible
            var currentItem = CurrentItem;

            // re-create view
            _groups.Clear();
            var ie = Source as IEnumerable;
            if (ie != null)
            {
                foreach (var item in ie)
                {
                    _groups.Add(new ListCollectionViewGroup(item));
                }
            }

            _groups.RaiseVectorChanged(new VectorChangedEventArgs(CollectionChange.Reset));
            RaiseVectorChanged(new VectorChangedEventArgs(CollectionChange.Reset));

            ////// keep selection if possible
            ////var currentItem = CurrentItem;

            ////// re-create view
            ////_view.Clear();
            ////var ie = Source as IEnumerable;
            ////if (ie != null)
            ////{
            ////    foreach (var item in ie)
            ////    {
            ////        _view.Add(item);
            ////    }
            ////}

            ////// notify listeners
            ////RaiseVectorChanged(VectorChangedEventArgs.Reset);

            ////// restore selection if possible
            ////MoveCurrentTo(currentItem);
        }

        // update view after an item changes (apply filter/sort if necessary)
        private void HandleItemChanged(object item)
        {
            //// apply filter/sort after edits
            //bool refresh = false;
            //if (_filter != null && !_filter(item))
            //{
            //    // item was removed from view
            //    refresh = true;
            //}

            //if (_sort.Count > 0)
            //{
            //    // find sorted index for this object
            //    _sortProps.Clear();
            //    var newIndex = _view.BinarySearch(item, this);
            //    if (newIndex < 0) newIndex = ~newIndex;

            //    // item moved within the collection
            //    if (newIndex >= _view.Count || _view[newIndex] != item)
            //    {
            //        refresh = true;
            //    }
            //    else if (newIndex < _view.Count && _view[newIndex] == item)
            //    {
            //        var comparer = (IComparer<object>)this;
            //        if (newIndex > 0 &&
            //            comparer.Compare(_view[newIndex - 1], _view[newIndex]) > 0)
            //        {
            //            refresh = true;
            //        }
            //        else if (newIndex < _view.Count - 1 &&
            //            comparer.Compare(_view[newIndex + 1], _view[newIndex]) < 0)
            //        {
            //            refresh = true;
            //        }
            //    }
            //}

            //if (refresh)
            //{
            //    HandleSourceChanged();
            //}
        }

        // move the cursor to a new position
        private bool MoveCurrentToIndex(int index)
        {
            // invalid?
            if (index < -1 || index >= _view.Count)
            {
                return false;
            }

            // no change?
            if (index == _index)
            {
                return false;
            }

            // fire changing
            var e = new CurrentChangingEventArgs();
            OnCurrentChanging(e);
            if (e.Cancel)
            {
                return false;
            }

            // change and fire changed
            _index = index;
            OnCurrentChanged(null);
            return true;
        }

        // get the type of item in the source collection
        private Type GetItemType()
        {
            Type itemType = null;
            if (_source != null)
            {
                var type = _source.GetType();
                var args = type.GenericTypeArguments;
                if (args.Length == 1)
                {
                    itemType = args[0];
                }
                else if (_sourceList != null && _sourceList.Count > 0)
                {
                    var item = _sourceList[0];
                    itemType = item.GetType();
                }
            }

            return itemType;
        }

        #endregion

        #region ** nested classes

        /// <summary>
        /// Class that implements IVectorChangedEventArgs so we can fire VectorChanged events.
        /// </summary>
        public class VectorChangedEventArgs : IVectorChangedEventArgs
        {
            public static VectorChangedEventArgs Reset { get; } = new VectorChangedEventArgs(CollectionChange.Reset, 0);

            public VectorChangedEventArgs(CollectionChange change, int index = -1)
            {
                CollectionChange = change;
                Index = (uint)index;
            }

            public CollectionChange CollectionChange { get; private set; }

            public uint Index { get; private set; }
        }

        #endregion

        #region ** ICollectionView

        /// <summary>
        /// Occurs after the current item has changed.
        /// </summary>
        public event EventHandler<object> CurrentChanged;

        /// <summary>
        /// Occurs before the current item changes.
        /// </summary>
        public event CurrentChangingEventHandler CurrentChanging;

        /// <summary>
        /// Occurs when the view collection changes.
        /// </summary>
        public event VectorChangedEventHandler<object> VectorChanged;

        /// <summary>
        /// Gets a colletion of top level groups.
        /// </summary>
        public IObservableVector<object> CollectionGroups
        {
            //get { return null; }
            get
            {
                return _groups;
            }
        }

        /// <summary>
        /// Gets the current item in the view.
        /// </summary>
        public object CurrentItem
        {
            get { return _index > -1 && _index < _view.Count ? _view[_index] : null; }
            set { MoveCurrentTo(value); }
        }

        /// <summary>
        /// Gets the ordinal position of the current item in the view.
        /// </summary>
        public int CurrentPosition
        {
            get { return _index; }
        }

        public bool IsCurrentAfterLast
        {
            get { return _index >= _view.Count; }
        }

        public bool IsCurrentBeforeFirst
        {
            get { return _index < 0; }
        }

        public bool MoveCurrentToFirst()
        {
            return MoveCurrentToIndex(0);
        }

        public bool MoveCurrentToLast()
        {
            return MoveCurrentToIndex(_view.Count - 1);
        }

        public bool MoveCurrentToNext()
        {
            return MoveCurrentToIndex(_index + 1);
        }

        public bool MoveCurrentToPosition(int index)
        {
            return MoveCurrentToIndex(index);
        }

        public bool MoveCurrentToPrevious()
        {
            return MoveCurrentToIndex(_index - 1);
        }

        public int IndexOf(object item)
        {
            return _view.IndexOf(item);
        }

        public bool MoveCurrentTo(object item)
        {
            return item != CurrentItem ? MoveCurrentToIndex(IndexOf(item)) : true;
        }

        // async operations not supported
        public bool HasMoreItems
        {
            get
            {
                var support = Source as ISupportIncrementalLoading;
                if (support != null)
                {
                    return support.HasMoreItems;
                }

                return false;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var support = Source as ISupportIncrementalLoading;
            if (support != null)
            {
                return support.LoadMoreItemsAsync(count);
            }

            throw new NotSupportedException();
        }

        // list operations

        public bool IsReadOnly
        {
            get { return _sourceList == null || _sourceList.IsReadOnly; }
        }

        public void Add(object item)
        {
            CheckReadOnly();
            _sourceList.Add(item);
        }

        public void Insert(int index, object item)
        {
            CheckReadOnly();
            _sourceList.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Remove(_view[index]);
        }

        public bool Remove(object item)
        {
            CheckReadOnly();
            _sourceList.Remove(item);
            return true;
        }

        public void Clear()
        {
            CheckReadOnly();
            _sourceList.Clear();
        }

        private void CheckReadOnly()
        {
            if (IsReadOnly)
            {
                throw new Exception("The source collection cannot be modified.");
            }
        }

        public object this[int index]
        {
            get { return _view[index]; }
            set { _view[index] = value; }
        }

        public bool Contains(object item)
        {
            return _view.Contains(item);
        }

        public void CopyTo(object[] array, int arrayIndex)
        {
            _view.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _view.Count; }
        }

        public IEnumerator<object> GetEnumerator()
        {
            return _view.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _view.GetEnumerator();
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class ObservableVector<T> : ObservableCollection<T>, IObservableVector<T>
    {
        public ObservableVector()
        {

        }

        public ObservableVector(IEnumerable source)
            : base(source.Cast<T>())
        {

        }

        public event VectorChangedEventHandler<T> VectorChanged;

        public void RaiseVectorChanged(IVectorChangedEventArgs args)
        {
            VectorChanged?.Invoke(this, args);
        }
    }

    public class ListCollectionViewGroup : ICollectionViewGroup
    {
        public ListCollectionViewGroup(object group)
        {
            Group = group;
            GroupItems = new ObservableVector<object>(group as IEnumerable);
        }

        public object Group { get; private set; }

        public IObservableVector<object> GroupItems { get; private set; }

        //public IObservableVector<object> GroupItems { get; private set; }
    }
}
