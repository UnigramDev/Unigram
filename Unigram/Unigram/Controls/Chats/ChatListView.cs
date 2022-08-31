using LinqToVisualTree;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.Logs;
using Unigram.ViewModels;
using Windows.Devices.Input;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls.Chats
{
    public class ChatListView : SelectListView
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ScrollViewer ScrollingHost { get; private set; }
        public ItemsStackPanel ItemsStack { get; private set; }

        public bool IsBottomReached => ScrollingHost?.VerticalOffset == ScrollingHost?.ScrollableHeight;

        public Action<bool> ViewVisibleMessages { get; set; }

        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private bool _loadingMore = false;

        private bool _programmaticExternal;
        private bool _programmaticScrolling;

        public bool IsProgrammaticScrolling
        {
            get => _programmaticExternal || _programmaticScrolling;
            set => _programmaticExternal = value;
        }

        public ChatListView()
        {
            DefaultStyleKey = typeof(ListView);

            _recognizer = new GestureRecognizer();
            _recognizer.GestureSettings = GestureSettings.DoubleTap;
            _recognizer.Tapped += Recognizer_Tapped;


            Loaded += OnLoaded;
        }

        public void ScrollToBottom()
        {
            if (ScrollingHost != null)
            {
                ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.SizeChanged += Panel_SizeChanged;

                SetScrollMode();
            }

            ViewChanged();
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;

            base.OnApplyTemplate();
        }

        private void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewChanged();
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ViewChanged();
        }

        private async void ViewChanged()
        {
            if (ScrollingHost == null || ItemsStack == null || ViewModel == null || _loadingMore)
            {
                return;
            }

            _loadingMore = true;

            //if (ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
            //if (ItemsStack.FirstCacheIndex == 0 && !e.IsIntermediate)
            using (await _loadMoreLock.WaitAsync())
            {
                var lastSlice = ViewModel.IsLastSliceLoaded != true;
                var firstSlice = ViewModel.IsFirstSliceLoaded != true;

                if (ItemsStack.ScrollingDirection == PanelScrollingDirection.Backward
                    && ItemsStack.FirstCacheIndex == 0
                    && lastSlice)
                {
                    Logger.Debug(LogTarget.Chat, "Going Backward, first cache index is minValue");
                    await ViewModel.LoadNextSliceAsync(true);
                }
                //else if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0)
                else if (ItemsStack.ScrollingDirection == PanelScrollingDirection.Forward
                    && ItemsStack.LastCacheIndex == ViewModel.Items.Count - 1)
                {
                    if (firstSlice)
                    {
                        Logger.Debug(LogTarget.Chat, "Going Forward, last cache index is maxValue");
                        await ViewModel.LoadPreviousSliceAsync(true);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                    }
                }
                else if (ItemsStack.ScrollingDirection == PanelScrollingDirection.None)
                {
                    if (lastSlice && ItemsStack.FirstVisibleIndex == 0)
                    {
                        Logger.Debug(LogTarget.Chat, "Going Nowhere, first visible index is minValue");
                        await ViewModel.LoadNextSliceAsync(true);
                    }

                    if (firstSlice && ItemsStack.LastCacheIndex == ViewModel.Items.Count - 1)
                    {
                        Logger.Debug(LogTarget.Chat, "Going Nowhere, last visible index is maxValue");
                        await ViewModel.LoadPreviousSliceAsync(true);
                    }
                }
            }

            _loadingMore = false;
        }

        private ItemsUpdatingScrollMode _currentMode;
        private ItemsUpdatingScrollMode? _pendingMode;
        private bool? _pendingForce;

        public ItemsUpdatingScrollMode ScrollMode => ItemsStack?.ItemsUpdatingScrollMode ?? _currentMode;

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

            if (_currentMode == _pendingMode)
            {
                _pendingMode = null;
                _pendingForce = null;
                return;
            }

            if (mode == ItemsUpdatingScrollMode.KeepItemsInView && (force || scroll.VerticalOffset < 200))
            {
                if (panel.ItemsUpdatingScrollMode != mode)
                {
                    Logger.Debug(LogTarget.Chat, "Changed scrolling mode to KeepItemsInView");
                    panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepItemsInView;
                }
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 200))
            {
                if (panel.ItemsUpdatingScrollMode != mode)
                {
                    Logger.Debug(LogTarget.Chat, "Changed scrolling mode to KeepLastItemInView");
                    panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListViewItem(this);
        }

        public async Task ScrollToItem(MessageViewModel item, VerticalAlignment alignment, bool highlight, double? pixel = null, ScrollIntoViewAlignment direction = ScrollIntoViewAlignment.Leading, bool? disableAnimation = null)
        {
            var scrollViewer = ScrollingHost;
            if (scrollViewer == null)
            {
                Logger.Debug(LogTarget.Chat, "ScrollingHost == null");
                goto Exit;
            }

            _programmaticScrolling = true;
            await ScrollIntoViewAsync(item, direction);

            var selectorItem = ContainerFromItem(item) as SelectorItem;
            if (selectorItem == null)
            {
                Logger.Debug(LogTarget.Chat, "selectorItem == null, abort");
                goto Exit;
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

            if (highlight)
            {
                var bubble = selectorItem.Descendants<MessageBubble>().FirstOrDefault();
                if (bubble != null)
                {
                    bubble.Highlight();
                }
            }

            if (scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight || position.Y < scrollViewer.ScrollableHeight)
            {
                if (scrollViewer.VerticalOffset.AlmostEquals(position.Y))
                {
                    goto Exit;
                }

                await scrollViewer.ChangeViewAsync(null, position.Y, disableAnimation ?? alignment != VerticalAlignment.Center, false);
            }

        Exit:
            _programmaticScrolling = _programmaticExternal = false;

            ViewVisibleMessages?.Invoke(true);
            ViewChanged();
        }

        public async Task ScrollIntoViewAsync(MessageViewModel item, ScrollIntoViewAlignment alignment)
        {
            var index = ViewModel.Items.IndexOf(item);
            var stack = ItemsStack;

            if (stack == null || index >= ItemsStack.FirstCacheIndex && index <= ItemsStack.LastCacheIndex)
            {
                return;
            }

            var tcs = new TaskCompletionSource<object>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            void viewChanged(object s1, ScrollViewerViewChangedEventArgs e1)
            {
                if (e1.IsIntermediate is false)
                {
                    LayoutUpdated += layoutUpdated;
                    ScrollingHost.ViewChanged -= viewChanged;
                }
            }

            try
            {
                ScrollIntoView(item, alignment);
                ScrollingHost.ViewChanged += viewChanged;

                await tcs.Task;
            }
            finally
            {
                LayoutUpdated -= layoutUpdated;
            }
        }

        #region Selection

        public bool IsSelectionEnabled
        {
            get { return (bool)GetValue(IsSelectionEnabledProperty); }
            set { SetValue(IsSelectionEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSelectionEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSelectionEnabledProperty =
            DependencyProperty.Register("IsSelectionEnabled", typeof(bool), typeof(ChatListView), new PropertyMetadata(false, OnSelectionEnabledChanged));

        private static void OnSelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatListView)d).OnSelectionEnabledChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        private void OnSelectionEnabledChanged(bool oldValue, bool newValue)
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

                var content = container.ContentTemplateRoot as MessageSelector;
                if (content != null)
                {
                    content.UpdateSelectionEnabled(newValue, true);
                }
            }
        }

        private MessageViewModel _firstItem;
        private MessageViewModel _lastItem;
        private bool _operation;
        private SelectionDirection _direction;

        private bool _pressed;
        private Point _position;

        private readonly GestureRecognizer _recognizer;

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

        protected void OnDoubleTapped(SelectorItem selector)
        {
            if (selector.ContentTemplateRoot is FrameworkElement root && root.Tag is MessageViewModel message)
            {
                ViewModel.ReplyToMessage(message);
            }
        }

        internal void OnPointerPressed(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if (IsSelectionEnabled is false)
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

        internal void OnPointerEntered(LazoListViewItem item, PointerRoutedEventArgs e)
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

            if (IsSelectionEnabled is false)
            {
                IsSelectionEnabled = true;
            }

            var message = ItemFromContainer(item) as MessageViewModel;
            if (message == null)
            {
                return;
            }

            if (_direction == SelectionDirection.None)
            {
                _direction = message.Id > _firstItem.Id
                    ? SelectionDirection.Down
                    : SelectionDirection.Up;
            }

            var direction = message.Id > _lastItem.Id
                ? SelectionDirection.Down
                : SelectionDirection.Up;

            if (direction != SelectionDirection.None)
            {
                var begin = Items.IndexOf(_firstItem);
                if (begin < 0)
                {
                    return;
                }

                var index = IndexFromContainer(item);
                var first = Math.Min(begin, index);
                var last = Math.Max(begin, index);

                for (int i = first; i <= last; i++)
                {
                    var current = Items[i] as MessageViewModel;

                    if (_operation)
                    {
                        ViewModel.Select(current);
                    }
                    else if (!_operation)
                    {
                        ViewModel.Unselect(current);
                    }
                }

                if (direction != _direction)
                {
                    if (_operation)
                    {
                        ViewModel.Unselect(_lastItem);
                    }
                    else if (!_operation)
                    {
                        ViewModel.Select(_lastItem);
                    }
                }
            }

            _lastItem = message;
        }

        internal void OnPointerMoved(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if (!_pressed || !e.Pointer.IsInContact || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            var message = ItemFromContainer(item) as MessageViewModel;
            if (message == null)
            {
                return;
            }

            if (_firstItem != null && _firstItem != message)
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
                _firstItem = _lastItem = message;
                _operation = !ViewModel.SelectedItems.ContainsKey(message.Id);

                _position = point.Position;
            }
            else if (_firstItem == message)
            {
                var contains = ViewModel.SelectedItems.ContainsKey(message.Id);

                var delta = Math.Abs(point.Position.Y - _position.Y);
                if (delta > 10)
                {
                    if (_operation && !contains)
                    {
                        ViewModel.Select(message);
                    }
                    else if (!_operation && contains)
                    {
                        ViewModel.Unselect(message);
                    }

                    IsSelectionEnabled = true;

                    if (item.ContentTemplateRoot is MessageSelector selector)
                    {
                        selector.ReleasePointerCapture(e.Pointer);
                    }
                }
                else
                {
                    if (_operation && contains)
                    {
                        ViewModel.Unselect(message);
                    }
                    else if (!_operation && !contains)
                    {
                        ViewModel.Select(message);
                    }

                    _direction = SelectionDirection.None;
                    _lastItem = message;
                }
            }
        }

        internal void OnPointerReleased(LazoListViewItem item, PointerRoutedEventArgs e)
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

            var handled = _firstItem != null && ViewModel.SelectedItems.ContainsKey(_firstItem.Id) == _operation;

            _firstItem = null;
            _lastItem = null;

            _pressed = false;
            _position = new Point();

            if (IsSelectionEnabled is false)
            {
                return;
            }

            if (ViewModel.SelectedItems.Count < 1)
            {
                IsSelectionEnabled = false;
            }

            e.Handled = handled;
        }

        internal void OnPointerCanceled(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            _recognizer.CompleteGesture();
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

        enum SelectionDirection
        {
            None,
            Up,
            Down,
        }

        #endregion
    }
}
