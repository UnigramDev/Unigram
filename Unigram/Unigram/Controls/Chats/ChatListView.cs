using LinqToVisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
{
    public class ChatListView : SelectListView, ILazoListView
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ScrollViewer ScrollingHost { get; private set; }
        public ItemsStackPanel ItemsStack { get; private set; }

        private DisposableMutex _loadMoreLock = new DisposableMutex();

        public ChatListView()
        {
            DefaultStyleKey = typeof(ListView);

            _recognizer = new GestureRecognizer();
            _recognizer.GestureSettings = GestureSettings.DoubleTap;
            _recognizer.Tapped += Recognizer_Tapped;

            Loaded += OnLoaded;
        }

        #region Selection

        public bool IsSelectionEnabled
        {
            get { return (bool)GetValue(IsSelectionEnabledProperty); }
            set { SetValue(IsSelectionEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsSelectionEnabledProperty =
            DependencyProperty.Register("IsSelectionEnabled", typeof(bool), typeof(ChatListView), new PropertyMetadata(false, OnSelectionEnabledChanged));

        private static void OnSelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatListView)d).OnSelectionEnabledChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnSelectionEnabledChanged(bool newValue, bool oldValue)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as ChatListViewItemPresenter;
                if (content != null)
                {
                    content.SetSelectionEnabled(newValue, true);
                }
            }

            if (newValue != oldValue)
            {
                ViewModel.SelectedItems.Clear();
            }
        }

        public void SelectRange2(ItemIndexRange range)
        {
            for (int i = range.FirstIndex; i <= range.LastIndex; i++)
            {
                var container = ContainerFromIndex(i) as SelectorItem;
                if (container != null && container.ContentTemplateRoot is ChatListViewItemPresenter presenter)
                {
                    presenter.SetSelected(true);
                }
            }
        }

        public void DeselectRange2(ItemIndexRange range)
        {
            for (int i = range.FirstIndex; i <= range.LastIndex; i++)
            {
                var container = ContainerFromIndex(i) as SelectorItem;
                if (container != null && container.ContentTemplateRoot is ChatListViewItemPresenter presenter)
                {
                    presenter.SetSelected(false);
                }
            }
        }

        public void SelectItem2(object item)
        {
            var container = ContainerFromItem(item) as SelectorItem;
            if (container != null && container.ContentTemplateRoot is ChatListViewItemPresenter presenter)
            {
                presenter.SetSelected(true);
            }
        }

        public void DeselectItem2(object item)
        {
            var container = ContainerFromItem(item) as SelectorItem;
            if (container != null && container.ContentTemplateRoot is ChatListViewItemPresenter presenter)
            {
                presenter.SetSelected(false);
            }
        }

        #endregion

        protected void OnDoubleTapped(SelectorItem selector)
        {
            if (selector.ContentTemplateRoot is FrameworkElement root && root.Tag is MessageViewModel message)
            {
                ViewModel.ReplyToMessage(message);
            }
        }

        public void ScrollToBottom()
        {
            if (ScrollingHost != null)
            {
                ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.SizeChanged += Panel_SizeChanged;

                SetScrollMode();
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (ScrollingHost.ScrollableHeight < 200 && Items.Count > 0)
                {
                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(false, true);
                    }

                    await ViewModel.LoadNextSliceAsync(false, true);
                }
            }
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;

            base.OnApplyTemplate();
        }

        private async void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (ScrollingHost.ScrollableHeight < 200)
                {
                    if (ViewModel.IsLastSliceLoaded != true)
                    {
                        await ViewModel.LoadNextSliceAsync(false, true);
                    }

                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(false, true);
                    }
                }
            }
        }

        private async void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ScrollingHost == null || ItemsStack == null || ViewModel == null)
            {
                return;
            }

            //if (ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
            //if (ItemsStack.FirstCacheIndex == 0 && !e.IsIntermediate)
            using (await _loadMoreLock.WaitAsync())
            {
                if (ItemsStack.FirstCacheIndex == 0 && !e.IsIntermediate)
                {
                    await ViewModel.LoadNextSliceAsync(true);
                }
                else if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
                {
                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(true);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }
                }
            }
        }

        private ItemsUpdatingScrollMode? _pendingMode;
        private bool? _pendingForce;

        public void SetScrollMode()
        {
            if (_pendingMode is ItemsUpdatingScrollMode mode && _pendingForce is bool force)
            {
                _pendingMode = null;
                _pendingForce = null;

                SetScrollMode(mode, force);
            }
        }

        public void SetScrollMode(ItemsUpdatingScrollMode mode, bool force)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                _pendingMode = mode;
                _pendingForce = force;

                return;
            }

            var scroll = ScrollingHost;
            if (scroll == null)
            {
                _pendingMode = mode;
                _pendingForce = force;

                return;
            }

            if (mode == ItemsUpdatingScrollMode.KeepItemsInView && (force || scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepItemsInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepLastItemInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListViewItem(this);
        }

        public async Task ScrollToItem(object item, VerticalAlignment alignment, bool highlight, double? pixel = null, ScrollIntoViewAlignment direction = ScrollIntoViewAlignment.Leading, bool? disableAnimation = null)
        {
            var scrollViewer = ScrollingHost;
            if (scrollViewer == null)
            {
                Logs.Logger.Debug(Logs.Target.Chat, "ScrollingHost == null");

                return;
            }

            // We are going to try two times, as the first one seem to fail sometimes
            // leading the chat to the wrong scrolling position
            var iter = 2;

            var selectorItem = ContainerFromItem(item) as SelectorItem;
            while (selectorItem == null && iter > 0)
            {
                Logs.Logger.Debug(Logs.Target.Chat, string.Format("selectorItem == null, {0} try", iter + 1));

                // call task-based ScrollIntoViewAsync to realize the item
                await this.ScrollIntoViewAsync(item, direction);

                // this time the item shouldn't be null again
                selectorItem = (SelectorItem)ContainerFromItem(item);
                iter--;
            }

            if (selectorItem == null)
            {
                Logs.Logger.Debug(Logs.Target.Chat, "selectorItem == null, abort");
                return;
            }

            // calculate the position object in order to know how much to scroll to
            var transform = selectorItem.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));

            if (alignment == VerticalAlignment.Top)
            {
                if (pixel is double adjust)
                {
                    position.Y -= adjust;
                }
            }
            else if (alignment == VerticalAlignment.Center)
            {
                if (selectorItem.ActualHeight < ActualHeight - 48)
                {
                    position.Y -= (ActualHeight - selectorItem.ActualHeight) / 2d;
                }
                else
                {
                    position.Y -= 48 + 4;
                }
            }
            else if (alignment == VerticalAlignment.Bottom)
            {
                position.Y -= ActualHeight - selectorItem.ActualHeight;

                if (pixel is double adjust)
                {
                    position.Y += adjust;
                }
            }

            // scroll to desired position with animation!
            scrollViewer.ChangeView(null, position.Y, null, disableAnimation ?? alignment != VerticalAlignment.Center);

            if (highlight)
            {
                var bubble = selectorItem.Descendants<MessageBubble>().FirstOrDefault() as MessageBubble;
                if (bubble == null)
                {
                    return;
                }

                bubble.Highlight();
            }
        }




        #region Lazo

        private long[] _ranges;
        private object _firstItem;
        private bool _operation;

        private bool _pressed;
        private Point _position;

        private GestureRecognizer _recognizer;

        public void OnPointerPressed(SelectorItem item, PointerRoutedEventArgs e)
        {
            if (IsSelectionEnabled == false)
            {
                var point = e.GetCurrentPoint(Window.Current.Content as FrameworkElement);
                if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                {
                    try
                    {
                        _recognizer.ProcessDownEvent(point);
                    }
                    catch
                    {
                        _recognizer.CompleteGesture();
                    }
                }
            }

            _pressed = true;
        }

        public void OnPointerEntered(SelectorItem item, PointerRoutedEventArgs e)
        {
            if (_firstItem == null || !_pressed || !e.Pointer.IsInContact /*|| SelectionMode != ListViewSelectionMode.Multiple*/ || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            var point = e.GetCurrentPoint(item);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (IsSelectionEnabled == false)
            {
                IsSelectionEnabled = true;
            }

            var begin = Items.IndexOf(_firstItem);
            if (begin < 0)
            {
                return;
            }

            var index = IndexFromContainer(item);
            var first = Math.Min(begin, index);
            var last = Math.Max(begin, index);

            if (ViewModel.SelectedItems.Count > 0)
            {
                ViewModel.SelectedItems.Clear();

                foreach (var original in _ranges)
                {
                    var message = ViewModel.Items.FirstOrDefault(x => x.Id == original);
                    if (message != null)
                    {
                        SelectItem2(message);
                    }
                }
            }

            if (_operation)
            {
                SelectRange2(new ItemIndexRange(first, (uint)Math.Max(1, last - first + 1)));
            }
            else
            {
                DeselectRange2(new ItemIndexRange(first, (uint)Math.Max(1, last - first + 1)));
            }
        }

        public void OnPointerMoved(SelectorItem item, PointerRoutedEventArgs e)
        {
            var child = ItemFromContainer(item) as MessageViewModel;
            if (child == null)
            {
                return;
            }

            if ((_firstItem != null && _firstItem != child) || !_pressed || !e.Pointer.IsInContact || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            var point = e.GetCurrentPoint(item);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (_firstItem == null)
            {
                _firstItem = ItemFromContainer(item);
                _ranges = ViewModel.SelectedItems.Keys.ToArray();
                _operation = !ViewModel.SelectedItems.ContainsKey(child.Id);

                _position = point.Position;
            }
            else if (_firstItem == child)
            {
                //var contains = SelectedItems.Contains(child);
                var contains = ViewModel.SelectedItems.ContainsKey(child.Id);

                var delta = Math.Abs(point.Position.Y - _position.Y);
                if (delta > 10)
                {
                    IsSelectionEnabled = true;

                    if (_operation && !contains)
                    {
                        SelectItem2(child);
                    }
                    else if (!_operation && contains)
                    {
                        DeselectItem2(child);
                    }
                }
                else
                {
                    if (_operation && contains)
                    {
                        DeselectItem2(child);
                    }
                    else if (!_operation && !contains)
                    {
                        SelectItem2(child);
                    }
                }
            }
        }

        public void OnPointerReleased(SelectorItem item, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(Window.Current.Content as FrameworkElement);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                try
                {
                    _recognizer.ProcessUpEvent(point);
                }
                catch
                {
                    _recognizer.CompleteGesture();
                }
            }

            var first = _firstItem != null ? ContainerFromItem(_firstItem) as SelectorItem : null;
            var child = first != null ? ItemFromContainer(first) as MessageViewModel : null;
            var handled = child != null && ViewModel.SelectedItems.ContainsKey(child.Id) == _operation;

            _firstItem = null;
            _ranges = null;

            _pressed = false;
            _position = new Point();

            if (IsSelectionEnabled == false)
            {
                return;
            }

            if (ViewModel.SelectedItems.Count < 1 && handled)
            {
                IsSelectionEnabled = false;
            }

            e.Handled = handled;
        }

        public void OnPointerCanceled(SelectorItem item, PointerRoutedEventArgs e)
        {
            _recognizer.CompleteGesture();
        }



        private void Recognizer_Tapped(GestureRecognizer sender, TappedEventArgs args)
        {
            if (args.TapCount == 2 && args.PointerDeviceType == PointerDeviceType.Mouse)
            {
                sender.CompleteGesture();

                var children = VisualTreeHelper.FindElementsInHostCoordinates(args.Position, this);
                var selector = children?.FirstOrDefault(x => x is SelectorItem) as SelectorItem;
                if (selector != null)
                {
                    OnDoubleTapped(selector);
                }
            }
        }



        protected bool CantSelect(object item)
        {
            return item is MessageViewModel message && message.IsService();
        }

        protected long IdFromContainer(DependencyObject container)
        {
            var item = ItemFromContainer(container) as MessageViewModel;
            if (item != null)
            {
                return item.Id;
            }

            return -1;
        }

        #endregion
    }
}
