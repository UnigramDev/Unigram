using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Telegram.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public enum SelectedItemsMode
    {
        None,
        Auto
    }

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
            if (e.OldValue is SelectedItemsBinder oldValue)
            {
                oldValue.UnsubscribeFromEvents();
            }

            if (e.NewValue is SelectedItemsBinder newValue)
            {
                newValue.Attach(d as ListViewBase);
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

            if (_listView.IsLoaded)
            {
                Transfer(SelectedItems as IList, _listView.SelectedItems);
            }
        }

        #endregion

        #region SelectionMode

        public SelectedItemsMode SelectionMode
        {
            get { return (SelectedItemsMode)GetValue(SelectionModeProperty); }
            set { SetValue(SelectionModeProperty, value); }
        }

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register("SelectionMode", typeof(SelectedItemsMode), typeof(SelectedItemsBinder), new PropertyMetadata(SelectedItemsMode.None));

        #endregion

        #region IsItemClickEnabled

        public bool IsItemClickEnabled
        {
            get { return (bool)GetValue(IsItemClickEnabledProperty); }
            set { SetValue(IsItemClickEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsItemClickEnabledProperty =
            DependencyProperty.Register("IsItemClickEnabled", typeof(bool), typeof(SelectedItemsBinder), new PropertyMetadata(false));

        #endregion

        private ListViewBase _listView;
        private FrameworkElementState _manager;

        private void Attach(ListViewBase view)
        {
            _listView = view;

            _manager = new FrameworkElementState(view);
            _manager.Loaded += OnLoaded;
            _manager.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Transfer(SelectedItems as IList, _listView.SelectedItems);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromEvents();
        }

        private void SelectedItems_CollectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Transfer(_listView.SelectedItems, SelectedItems as IList);

            if (SelectionMode == SelectedItemsMode.Auto)
            {
                if (_listView.SelectedItems.Count == 0 && _listView.SelectionMode == ListViewSelectionMode.Multiple)
                {
                    _listView.SelectionMode = ListViewSelectionMode.None;
                    _listView.IsItemClickEnabled = IsItemClickEnabled;
                }
            }
        }

        private void Context_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (SelectionMode == SelectedItemsMode.Auto && SelectedItems is IList list)
            {
                if (list.Count > 0 && _listView.SelectionMode == ListViewSelectionMode.None)
                {
                    _listView.SelectionMode = ListViewSelectionMode.Multiple;
                    _listView.IsItemClickEnabled = false;
                }
                else if (list.Count == 0 && _listView.SelectionMode == ListViewSelectionMode.Multiple)
                {
                    _listView.SelectionMode = ListViewSelectionMode.None;
                    _listView.IsItemClickEnabled = IsItemClickEnabled;
                }
            }

            Transfer(SelectedItems as IList, _listView.SelectedItems);
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

        private void Transfer(IEnumerable source, IEnumerable target)
        {
            UnsubscribeFromEvents();

            if (_listView.SelectionMode == ListViewSelectionMode.Multiple && source != null && target != null)
            {
                if (target is IMvxObservableCollection collection)
                {
                    collection.ReplaceWith(source);
                }
                else if (target is IList<object> list && source is IList last)
                {
                    try
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            object o = list[i];

                            if (last.Contains(o))
                                continue;

                            list.Remove(o);
                            i--;
                        }

                        foreach (var o in source)
                        {
                            if (list.Contains(o))
                                continue;

                            list.Add(o);
                        }
                    }
                    catch { }
                }
            }

            SubscribeToEvents(_listView);
        }
    }
}
