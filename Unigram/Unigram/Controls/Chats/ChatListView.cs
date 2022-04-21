using LinqToVisualTree;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Messages;
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.SizeChanged += Panel_SizeChanged;

                SetScrollMode();
            }

            _loadingMore = true;

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

            _loadingMore = false;
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;

            base.OnApplyTemplate();
        }

        private async void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _loadingMore = true;

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

            _loadingMore = false;
        }

        private async void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
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
                if (ItemsStack.FirstCacheIndex == 0 && ViewModel.IsLastSliceLoaded != true)
                {
                    await ViewModel.LoadNextSliceAsync(true);
                }
                //else if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0)
                else if (ItemsStack.LastCacheIndex == ViewModel.Items.Count - 1)
                {
                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(true);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.LogTarget.Chat, "Setting scroll mode to KeepLastItemInView");
                    }
                }
            }

            _loadingMore = false;
        }

        private ItemsUpdatingScrollMode _currentMode;
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

            if (_currentMode == _pendingMode)
            {
                _pendingMode = null;
                _pendingForce = null;
                return;
            }

            if (mode == ItemsUpdatingScrollMode.KeepItemsInView && (force || scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepItemsInView");

                panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepItemsInView;
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepLastItemInView");

                panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepLastItemInView;
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
                ViewVisibleMessages?.Invoke(true);

                Logs.Logger.Debug(Logs.LogTarget.Chat, "ScrollingHost == null");
                return;
            }

            _programmaticScrolling = true;

            ScrollIntoView(item, direction);
            await this.UpdateLayoutAsync();

            var selectorItem = ContainerFromItem(item) as SelectorItem;
            if (selectorItem == null)
            {
                ScrollIntoView(item, direction);
                await this.UpdateLayoutAsync();

                selectorItem = ContainerFromItem(item) as SelectorItem;
            }

            if (selectorItem == null)
            {
                _programmaticScrolling = _programmaticExternal = false;
                ViewVisibleMessages?.Invoke(true);

                Logs.Logger.Debug(Logs.LogTarget.Chat, "selectorItem == null, abort");
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
                await scrollViewer.ChangeViewAsync(null, position.Y, disableAnimation ?? alignment != VerticalAlignment.Center, false);
            }

            _programmaticScrolling = _programmaticExternal = false;
            ViewVisibleMessages?.Invoke(true);
        }

        #region Selection

        private bool _isSelectionEnabled;

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
            _isSelectionEnabled = newValue;

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
                var contains = ViewModel.SelectedItems.ContainsKey(message.Id);

                if (direction == _direction)
                {
                    if (_operation && !contains)
                    {
                        ViewModel.Select(message);
                        ViewModel.Select(_lastItem);
                    }
                    else if (!_operation && contains)
                    {
                        ViewModel.Unselect(message.Id);
                        ViewModel.Unselect(_lastItem.Id);
                    }
                }
                else if (direction != _direction)
                {
                    System.Diagnostics.Debug.WriteLine(" - Opposite direction");

                    if (!_operation && !contains)
                    {
                        ViewModel.Select(message);
                        ViewModel.Select(_lastItem);
                    }
                    else if (_operation && contains)
                    {
                        ViewModel.Unselect(message.Id);
                        ViewModel.Unselect(_lastItem.Id);
                    }
                }

                var last = ContainerFromItem(_lastItem) as SelectorItem;
                if (last?.ContentTemplateRoot is MessageSelector lastSelector)
                {
                    lastSelector.UpdateMessage(_lastItem);
                }

                if (item.ContentTemplateRoot is MessageSelector selector)
                {
                    selector.UpdateMessage(message);
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
                        ViewModel.Unselect(message.Id);
                    }

                    IsSelectionEnabled = true;

                    if (item.ContentTemplateRoot is MessageSelector selector)
                    {
                        selector.ReleasePointerCapture(e.Pointer);
                        selector.UpdateMessage(message);
                    }
                }
                else
                {
                    if (_operation && contains)
                    {
                        ViewModel.Unselect(message.Id);
                    }
                    else if (!_operation && !contains)
                    {
                        ViewModel.Select(message);
                    }

                    _direction = SelectionDirection.None;
                    _lastItem = message;

                    if (item.ContentTemplateRoot is MessageSelector selector)
                    {
                        selector.UpdateMessage(message);
                    }
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
