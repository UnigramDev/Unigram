using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.Collections;
using Telegram.Collections;

namespace Telegram.Common
{
    public class SelectedItemsBinder : DependencyObject
    {
        #region Attached

        public static SelectedItemsBinder GetAttached(DependencyObject obj)
        {
            return (SelectedItemsBinder)obj.GetValue(AttachedProperty);
        }

        public static void SetAttached(DependencyObject obj, SelectedItemsBinder value)
        {
            obj.SetValue(AttachedProperty, value);
        }

        public static readonly DependencyProperty AttachedProperty =
            DependencyProperty.RegisterAttached("Attached", typeof(SelectedItemsBinder), typeof(ListViewBase), new PropertyMetadata(null, OnSynchronizerChanged));

        private static void OnSynchronizerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is  SelectedItemsBinder oldValue)
            {
                oldValue.UnsubscribeFromEvents();
            }

            if (e.NewValue is SelectedItemsBinder newValue)
            {
                newValue.SubscribeToEvents(d as ListViewBase);
            }
        }

        #endregion

        #region SelectedItems

        public INotifyCollectionChanged SelectedItems
        {
            get { return (INotifyCollectionChanged)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(INotifyCollectionChanged), typeof(SelectedItemsBinder), new PropertyMetadata(null, OnSelectedItemsChanged));

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SelectedItemsBinder)d).OnSelectedItemsChanged((INotifyCollectionChanged)e.NewValue, (INotifyCollectionChanged)e.OldValue);
        }

        private void OnSelectedItemsChanged(INotifyCollectionChanged newValue, INotifyCollectionChanged oldValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged += Context_CollectionChanged;
            }

            UnsubscribeFromEvents();

            Transfer(SelectedItems as IList, _listView.SelectedItems);

            SubscribeToEvents(_listView);
        }

        #endregion

        private ListViewBase _listView;

        private void SelectedItems_CollectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UnsubscribeFromEvents();

            Transfer(_listView.SelectedItems, SelectedItems as IList);

            SubscribeToEvents(_listView);
        }

        private void Context_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UnsubscribeFromEvents();

            Transfer(SelectedItems as IList, _listView.SelectedItems);

            SubscribeToEvents(_listView);
        }

        protected void SubscribeToEvents(ListViewBase listView)
        {
            if (_listView != null)
            {
                _listView.SelectionChanged -= SelectedItems_CollectionChanged;
            }

            _listView = listView;

            if (_listView != null)
            {
                _listView.SelectionChanged += SelectedItems_CollectionChanged;
            }

            if (SelectedItems != null)
            {
                SelectedItems.CollectionChanged -= Context_CollectionChanged;
                SelectedItems.CollectionChanged += Context_CollectionChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_listView != null)
            {
                _listView.SelectionChanged -= SelectedItems_CollectionChanged;
            }

            if (SelectedItems != null)
            {
                SelectedItems.CollectionChanged -= Context_CollectionChanged;
            }
        }

        public static void Transfer(IEnumerable source, IEnumerable target)
        {
            if (source == null || target == null)
                return;

            if (target is IMvxObservableCollection collection)
            {
                collection.ReplaceWith(source);
            }
            else if (target is IList<object> list)
            {
                list.Clear();

                foreach (var o in source)
                {
                    list.Add(o);
                }
            }
        }
    }
}
