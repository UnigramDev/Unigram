//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Devices.Input;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Controls.Chats
{
    public class ChatHistoryView : ListViewEx
    {
        public DialogViewModel ViewModel { get; set; }
        public IDialogDelegate Delegate { get; set; }

        public ScrollViewer ScrollingHost { get; private set; }

        public bool IsBottomReached
        {
            get
            {
                if (ScrollingHost != null)
                {
                    return ScrollingHost.VerticalOffset.AlmostEquals(ScrollingHost.ScrollableHeight);
                }

                return true;
            }
        }

        private readonly DisposableMutex _loadMoreLock = new();
        private readonly SemaphoreSlim _loadMoreSemaphore = new(2, 2);

        private TaskCompletionSource<bool> _waitItemsPanelRoot = new();

        public PanelScrollingDirection ScrollingDirection { get; private set; }

        public ChatHistoryView()
        {
            DefaultStyleKey = typeof(ListView);

            _recognizer = new GestureRecognizer();
            _recognizer.GestureSettings = GestureSettings.DoubleTap;

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        private bool _raiseViewChanged;
        public event EventHandler<ScrollViewerViewChangedEventArgs> ViewChanged;

        public void ScrollToBottom()
        {
            ScrollingHost?.ChangeView(null, ScrollingHost.ScrollableHeight, null);
        }

        public bool IsSuspended => !_raiseViewChanged;

        public void Suspend()
        {
            _raiseViewChanged = false;
        }

        public void Resume()
        {
            _raiseViewChanged = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _recognizer.Tapped += Recognizer_Tapped;

            if (ItemsPanelRoot != null)
            {
                ItemsPanelRoot.SizeChanged += OnSizeChanged;

                _waitItemsPanelRoot.TrySetResult(true);
                SetScrollingMode();
            }

            ViewChanging();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Logger.Info($"ItemsPanelRoot.Children.Count: {ItemsPanelRoot?.Children.Count}");
            Logger.Info($"Items.Count: {Items.Count}");

            _recognizer.Tapped -= Recognizer_Tapped;

            if (ItemsPanelRoot != null)
            {
                ItemsPanelRoot.SizeChanged -= OnSizeChanged;
            }

            _waitItemsPanelRoot = new();
            _raiseViewChanged = false;

            // Note, this is done because of the following:
            // In some conditions (always?) ListView starts to store
            // all the created containers in the ItemsPanelRoot (on Unload presumably).
            // This causes an enormous overhead when moving from a different page to ChatPage,
            // as all the SelectorItem (some times they can be hundreds) will be measured arranged
            // right before all of them get unloaded again.
            // Setting ItemsSource to null seems to prevent this from happening.
            // IMPORTANT: this must only happen on Unload (so when closing the chat page).
            if (ItemsSource is ISynchronizedList source)
            {
                ItemsSource = null;
                source.Disconnect();
            }
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanging += OnViewChanging;
            ScrollingHost.ViewChanged += OnViewChanged;

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewChanging();
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_raiseViewChanged)
            {
                ViewChanged?.Invoke(sender, e);
            }

            if (e.IsIntermediate)
            {
                return;
            }

            ScrollingDirection = PanelScrollingDirection.None;
        }

        private void OnViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            ViewChanging(e.FinalView.VerticalOffset != e.NextView.VerticalOffset ?
                e.FinalView.VerticalOffset < e.NextView.VerticalOffset
                ? PanelScrollingDirection.Backward
                : PanelScrollingDirection.Forward
                : PanelScrollingDirection.None);
        }

        private async void ViewChanging(PanelScrollingDirection direction = PanelScrollingDirection.None)
        {
            ScrollingDirection = direction;

            if (ScrollingHost == null || ItemsPanelRoot is not ItemsStackPanel panel || ViewModel == null || !_loadMoreSemaphore.Wait(0))
            {
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (IsDisconnected)
                {
                    _loadMoreSemaphore.Release();
                    return;
                }

                var lastSlice = ViewModel.IsLastSliceLoaded != true;
                var firstSlice = ViewModel.IsFirstSliceLoaded != true;

                if (direction == PanelScrollingDirection.Backward
                    && panel.FirstCacheIndex == 0
                    && lastSlice)
                {
                    Logger.Debug($"Going {direction}, loading history in the past");
                    await ViewModel.LoadNextSliceAsync();
                }
                else if (direction == PanelScrollingDirection.Forward
                    && panel.LastCacheIndex == ViewModel.Items.Count - 1)
                {
                    await LoadPreviousSliceAsync(direction, firstSlice);
                }
                else if (direction == PanelScrollingDirection.None)
                {
                    if (lastSlice && panel.FirstVisibleIndex == 0)
                    {
                        Logger.Debug($"Going {direction}, loading history in the past");
                        await ViewModel.LoadNextSliceAsync();
                    }

                    if (panel.LastCacheIndex == ViewModel.Items.Count - 1)
                    {
                        await LoadPreviousSliceAsync(direction, firstSlice);
                    }
                }
            }

            _loadMoreSemaphore.Release();
        }

        private Task LoadPreviousSliceAsync(PanelScrollingDirection direction, bool firstSlice)
        {
            if (firstSlice)
            {
                Logger.Debug($"Going {direction}, loading history in the future");
                return ViewModel.LoadPreviousSliceAsync();
            }

            SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            return Task.CompletedTask;
        }

        private ItemsUpdatingScrollMode _currentMode;
        private ItemsUpdatingScrollMode? _pendingMode;
        private bool? _pendingForce;

        public void SetScrollingMode()
        {
            if (_pendingMode is ItemsUpdatingScrollMode mode && _pendingForce is bool force)
            {
                _pendingMode = null;
                _pendingForce = null;

                SetScrollingMode(mode, force);
            }
        }

        public void SetScrollingMode(ItemsUpdatingScrollMode mode, bool force)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            var scroll = ScrollingHost;

            if (panel == null || scroll == null)
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
                    Logger.Debug("Changed scrolling mode to KeepItemsInView");
                    panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepItemsInView;
                }
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 200))
            {
                if (panel.ItemsUpdatingScrollMode != mode)
                {
                    Logger.Debug("Changed scrolling mode to KeepLastItemInView");
                    panel.ItemsUpdatingScrollMode = _currentMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                }
            }
        }

        public async void ScrollToItem(MessageViewModel item, VerticalAlignment alignment, MessageBubbleHighlightOptions options, double? pixel = null, ScrollIntoViewAlignment direction = ScrollIntoViewAlignment.Leading, bool? disableAnimation = null, TaskCompletionSource<bool> tsc = null)
        {
            Suspend();

            var scrollViewer = ScrollingHost;
            var handler = Delegate;

            if (scrollViewer == null || handler == null)
            {
                Logger.Debug("ScrollingHost == null");
                goto Exit;
            }

            await ScrollIntoViewAsync(item, direction, true);

            var selectorItem = handler.ContainerFromItem(item.Id);
            if (selectorItem == null)
            {
                // TODO: experimental
                if (ViewModel.Items.ContainsKey(item.Id))
                {
                    Logger.Debug("selectorItem == null, but item is known, retry");

                    await ScrollIntoViewAsync(item, direction, false);
                    selectorItem = handler.ContainerFromItem(item.Id);
                }

                if (selectorItem == null)
                {
                    Logger.Debug("selectorItem == null, abort");
                    goto Exit;
                }
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

            if (options != null && options.Highlight)
            {
                if (selectorItem.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                {
                    bubble.Highlight(options);
                }
            }

            if (scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight || position.Y < scrollViewer.ScrollableHeight)
            {
                if (scrollViewer.VerticalOffset.AlmostEquals(position.Y))
                {
                    TryFocus(selectorItem, options);

                    goto Exit;
                }

                await scrollViewer.ChangeViewAsync(null, position.Y, disableAnimation ?? alignment != VerticalAlignment.Center, false);
            }

            TryFocus(selectorItem, options);

        Exit:
            Resume();

            if (scrollViewer != null)
            {
                ViewChanging();
                ViewChanged?.Invoke(scrollViewer, null);
            }

            tsc?.TrySetResult(true);
        }

        private void TryFocus(SelectorItem selectorItem, MessageBubbleHighlightOptions options)
        {
            try
            {
                if ((options == null || options.MoveFocus) && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    selectorItem.Focus(FocusState.Keyboard);
                }
            }
            catch
            {
                // Focus cannot be moved while getting or losing focus.
            }
        }

        private async Task ScrollIntoViewAsync(MessageViewModel item, ScrollIntoViewAlignment alignment, bool fastPath)
        {
            if (ItemsPanelRoot == null)
            {
                Logger.Info("ItemsPanelRoot == null");

                // Some actions cause IsItemsHostInvalid to become true.
                // If this is the case, ItemsPanelRoot will return null, and scrolling may not work.
                // The current code should not invalidate the ItemsHost, but if this happens, we try to be prepared.
                if (_waitItemsPanelRoot.Task.Status == TaskStatus.RanToCompletion)
                {
                    ScrollingHost.UpdateLayout();
                }
                else
                {
                    await _waitItemsPanelRoot.Task;
                }
            }

            var index = Items.IndexOf(item);
            var panel = ItemsPanelRoot as ItemsStackPanel;

            if (panel == null || index >= panel.FirstCacheIndex && index <= panel.LastCacheIndex)
            {
                Logger.Info("Skipping because " + (panel == null ? "null" : "cached"));
                return;
            }

            // Judging from WinUI 3 source code, calling UpdateLayout on the panel should be
            // enough to guarantee that the container we are scrolling to gets realized 
            // 1.4-stable/dxaml/xcp/dxaml/lib/ModernCollectionBasePanel_WindowManagement_Partial.cpp#L2138
            if (fastPath)
            {
                ScrollIntoView(item, alignment);
                panel.UpdateLayout();

                //if (index < panel.FirstCacheIndex || index > panel.LastCacheIndex)
                //{
                //    panel.UpdateLayout();
                //}
                //else
                //{
                //    Logger.Info("Item is in the cached range");
                //}

                return;
            }

            var tcs = new TaskCompletionSource<object>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            void viewChanged(object s1, ScrollViewerViewChangedEventArgs e1)
            {
                panel.LayoutUpdated -= layoutUpdated;

                if (e1.IsIntermediate is false)
                {
                    panel.LayoutUpdated += layoutUpdated;
                    ScrollingHost.ViewChanged -= viewChanged;
                }
            }

            try
            {
                ScrollIntoView(item, alignment);
                panel.LayoutUpdated += layoutUpdated;
                ScrollingHost.ViewChanged += viewChanged;

                await tcs.Task;
            }
            finally
            {
                panel.LayoutUpdated -= layoutUpdated;
                ScrollingHost.ViewChanged -= viewChanged;
            }
        }

        #region Selection

        public bool IsSelectionEnabled
        {
            get { return (bool)GetValue(IsSelectionEnabledProperty); }
            set { SetValue(IsSelectionEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsSelectionEnabledProperty =
            DependencyProperty.Register("IsSelectionEnabled", typeof(bool), typeof(ChatHistoryView), new PropertyMetadata(false, OnSelectionEnabledChanged));

        private static void OnSelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatHistoryView)d).OnSelectionEnabledChanged((bool)e.OldValue, (bool)e.NewValue);
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
                var container = ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as MessageSelector;
                content?.UpdateSelectionEnabled(newValue, true);
            }
        }

        private MessageViewModel _firstItem;
        private MessageViewModel _lastItem;
        private bool _operation;
        private SelectionDirection _direction;

        private bool _raised;
        private bool _pressed;
        private Point _position;

        private readonly GestureRecognizer _recognizer;

        private void Recognizer_Tapped(GestureRecognizer sender, TappedEventArgs args)
        {
            if (args.TapCount == 2 && args.PointerDeviceType == PointerDeviceType.Mouse)
            {
                _raised = true;
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
            var message = ItemFromContainer(selector) as MessageViewModel;
            if (message != null)
            {
                ViewModel.DoubleTapped(message, WindowContext.IsKeyDown(VirtualKey.Control));
            }
        }

        internal void OnPointerPressed(MessageSelector item, PointerRoutedEventArgs e)
        {
            if (IsSelectionEnabled is false && !_pressed)
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
                else if (_recognizer.IsActive)
                {
                    _recognizer.CompleteGesture();
                }
            }
            else if (_recognizer.IsActive)
            {
                _recognizer.CompleteGesture();
            }

            _pressed = !_raised;
            _raised = false;
        }

        internal void OnPointerEntered(MessageSelector item, PointerRoutedEventArgs e)
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

            var message = item.Message;
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

                var index = Items.IndexOf(message);
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

        internal void OnPointerMoved(MessageSelector item, PointerRoutedEventArgs e)
        {
            if (!_pressed || !e.Pointer.IsInContact || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                return;
            }

            var message = item.Message;
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
                    item.ReleasePointerCapture(e.Pointer);
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

        internal void OnPointerReleased(MessageSelector item, PointerRoutedEventArgs e)
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
            else if (_recognizer.IsActive)
            {
                _recognizer.CompleteGesture();
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

        internal void OnPointerCanceled(MessageSelector item, PointerRoutedEventArgs e)
        {
            _recognizer.CompleteGesture();
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
