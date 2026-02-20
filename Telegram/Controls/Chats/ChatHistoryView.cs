//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    public partial class ChatHistoryView : ListViewEx
    {
        public DialogViewModel ViewModel { get; set; }
        public IDialogDelegate Delegate { get; set; }

        public ScrollViewer ScrollingHost { get; private set; }
        public CompositionPropertySet ScrollingPropertySet { get; private set; }

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

        private readonly DispatcherTimer _scrollTracker = new();

        private TaskCompletionSource<bool> _waitItemsPanelRoot = new();

        public PanelScrollingDirection ScrollingDirection { get; private set; }

        public ChatHistoryView()
        {
            DefaultStyleKey = typeof(ListView);

            _scrollTracker = new();
            _scrollTracker.Interval = TimeSpan.FromMilliseconds(33);
            _scrollTracker.Tick += OnTick;

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        private bool _raiseViewChanged;
        public event EventHandler<ScrollViewerViewChangedEventArgs> ViewChanged;

        public void ScrollToBottom()
        {
            HasBeenScrolled = true;
            ScrollingHost?.TryChangeView(null, ScrollingHost.ScrollableHeight, null);
        }

        public bool IsSuspended => !_raiseViewChanged;

        public bool HasBeenScrolled { get; private set; }

        public void Suspend()
        {
            _raiseViewChanged = false;
            HasBeenScrolled = false;
        }

        public void Resume()
        {
            _raiseViewChanged = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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

            if (ItemsPanelRoot != null)
            {
                ItemsPanelRoot.SizeChanged -= OnSizeChanged;
            }

            _waitItemsPanelRoot.TrySetResult(false);
            _waitItemsPanelRoot = new();

            _raiseViewChanged = false;
        }

        public void Disconnect()
        {
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
            // TODO: Name
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");

            // Used by saved messages tab
            ScrollingHost ??= this.GetParent<ScrollViewer>();
            ScrollingHost.ViewChanging += OnViewChanging;
            ScrollingHost.ViewChanged += OnViewChanged;
            ScrollingHost.DirectManipulationStarted += OnDirectManipulationStarted;
            ScrollingHost.DirectManipulationCompleted += OnDirectManipulationCompleted;
            ScrollingHost.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);

            ScrollingPropertySet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);

            base.OnApplyTemplate();
        }

        private void OnDirectManipulationStarted(object sender, object e)
        {
            HasBeenScrolled = true;

            // TODO: only start timer if close to bottom
            if (ViewModel.PendingSponsoredMessage != null)
            {
                _scrollTracker.Start();
            }
        }

        private void OnDirectManipulationCompleted(object sender, object e)
        {
            _scrollTracker.Stop();
        }

        private void OnTick(object sender, object e)
        {
            var message = ViewModel.PendingSponsoredMessage;
            if (message == null)
            {
                _scrollTracker.Stop();
                return;
            }

            var offset = GetOverscrollOffset();
            if (offset < -1)
            {
                _scrollTracker.Stop();
                SetScrollingMode(ItemsUpdatingScrollMode.KeepItemsInView, true);

                ViewModel.PendingSponsoredMessage = null;
                ViewModel.InsertMessageInOrder(ViewModel.CreateMessage(new Message(message.MessageId, null, ViewModel.ChatId, null, null, false, false, false, false, false, true, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageSponsored(message), null)));
            }
        }

        private float GetOverscrollOffset()
        {
            if (ScrollingHost.VerticalOffset < ScrollingHost.ScrollableHeight || ViewModel.IsNewestSliceLoaded is not true)
            {
                return 1;
            }

            var itemsPanel = ItemsPanelRoot;
            if (itemsPanel != null)
            {
                var transform = itemsPanel.TransformToVisual(this);
                var point = transform.TransformVector2();

                return point.Y + itemsPanel.ActualSize.Y - ActualSize.Y;
            }

            return 1;
        }


        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            HasBeenScrolled = true;

            var modifiers = WindowContext.KeyModifiers();
            if (modifiers == VirtualKeyModifiers.Control)
            {
                try
                {
                    var presenter = ScrollingHost.GetChild<ScrollContentPresenter>();

                    var point = e.GetCurrentPoint(ScrollingHost);
                    if (point.Properties.MouseWheelDelta < 0)
                    {
                        presenter.PageDown();
                    }
                    else
                    {
                        presenter.PageUp();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }

            var message = ViewModel.PendingSponsoredMessage;
            if (message != null && ViewModel.IsNewestSliceLoaded is true && ScrollingHost.VerticalOffset.AlmostEquals(ScrollingHost.ScrollableHeight, 1e-02))
            {
                var point = e.GetCurrentPoint(ScrollingHost);
                if (point.Properties.MouseWheelDelta < 0)
                {
                    SetScrollingMode(ItemsUpdatingScrollMode.KeepItemsInView, true);

                    ViewModel.PendingSponsoredMessage = null;
                    ViewModel.InsertMessageInOrder(ViewModel.CreateMessage(new Message(message.MessageId, null, ViewModel.ChatId, null, null, false, false, false, false, false, true, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageSponsored(message), null)));
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingHost?.ScrollableHeight < ScrollingHost?.ViewportHeight)
            {
                ViewChanging();
            }
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
            var finalOffset = e.FinalView.VerticalOffset;
            var nextOffset = e.NextView.VerticalOffset;

            if (finalOffset == nextOffset && !e.IsInertial)
            {
                nextOffset = ScrollingHost.VerticalOffset;
            }

            ViewChanging(e.FinalView.VerticalOffset != nextOffset ?
                finalOffset < nextOffset
                ? PanelScrollingDirection.Backward
                : PanelScrollingDirection.Forward
                : PanelScrollingDirection.None);
        }

        public void ViewChanging(PanelScrollingDirection direction = PanelScrollingDirection.None)
        {
            ScrollingDirection = direction;

            if (ScrollingHost == null || ItemsPanelRoot is not ItemsStackPanel panel || ViewModel == null || IsDisconnected)
            {
                return;
            }

            var lastSlice = ViewModel.IsSavedMessagesTab ? ViewModel.IsNewestSliceLoaded != true : ViewModel.IsOldestSliceLoaded != true;
            var firstSlice = ViewModel.IsSavedMessagesTab ? ViewModel.IsOldestSliceLoaded != true : ViewModel.IsNewestSliceLoaded != true;

            if (direction != PanelScrollingDirection.Backward && panel.LastCacheIndex == ViewModel.Items.Count - 1)
            {
                LoadPreviousSlice(direction, firstSlice);
            }
        }

        private void LoadPreviousSlice(PanelScrollingDirection direction, bool firstSlice)
        {
            if (firstSlice)
            {
                return;
            }

            SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
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

            if (ViewModel.IsSavedMessagesTab)
            {
                mode = mode == ItemsUpdatingScrollMode.KeepLastItemInView
                    ? ItemsUpdatingScrollMode.KeepItemsInView
                    : ItemsUpdatingScrollMode.KeepLastItemInView;
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
            var transform = selectorItem.TransformToVisual(scrollViewer.ContentTemplateRoot);
            var position = transform.TransformPoint(new Point());

            if (alignment == VerticalAlignment.Top)
            {
                if (pixel is double adjust)
                {
                    position.Y -= adjust;
                }
            }
            else if (alignment == VerticalAlignment.Center)
            {
                Rect GetHighlightArea()
                {
                    if (options != null && options.Highlight)
                    {
                        if (selectorItem.ContentTemplateRoot is MessageSelector selector && selector.Content is MessageBubble bubble)
                        {
                            return bubble.Highlight(options);
                        }
                    }

                    return new Rect(0, 0, selectorItem.ActualWidth, selectorItem.ActualHeight);
                }

                var occludedHeight = Delegate.AnimatedHeight;
                var highlightArea = GetHighlightArea();

                if (highlightArea.Height < ActualHeight - occludedHeight)
                {
                    position.Y -= (ActualHeight / 2 - (highlightArea.Bottom - highlightArea.Height / 2)) + occludedHeight / 2;

                    if (Delegate.HasMessagesPadding)
                    {
                        position.Y += occludedHeight;
                    }
                }
                else
                {
                    position.Y -= occludedHeight;
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
                // Some actions cause IsItemsHostInvalid to become true.
                // If this is the case, ItemsPanelRoot will return null, and scrolling may not work.
                // The current code should not invalidate the ItemsHost, but if this happens, we try to be prepared.
                if (_waitItemsPanelRoot.Task.Status == TaskStatus.RanToCompletion)
                {
                    Logger.Info("ItemsPanelRoot == null, UpdateLayout");
                    ScrollingHost.UpdateLayout();
                }
                else
                {
                    Logger.Info("ItemsPanelRoot == null, Await");
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

        private bool _pressed;
        private Point _position;

        internal void OnDoubleTapped(MessageViewModel message, DoubleTappedRoutedEventArgs e)
        {
            _pressed = false;

            if (message != null && !ViewModel.IsSelectionEnabled)
            {
                e.Handled = true;
                ViewModel.DoubleTapped(message, WindowContext.IsKeyDown(VirtualKey.Control));
            }
        }

        internal void OnPointerPressed(MessageSelector item, PointerRoutedEventArgs e)
        {
            _pressed = true;
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
            var point = e.GetCurrentPoint(XamlRoot.Content);
            var handled = _firstItem != null && ViewModel.SelectedItems.ContainsKey(_firstItem.Id) == _operation;

            _firstItem = null;
            _lastItem = null;

            _pressed = false;
            _position = new Point();

            if (IsSelectionEnabled is false)
            {
                return;
            }

            if (ViewModel.SelectedItems.Count < 1 && ViewModel.IsReportingMessages == null)
            {
                IsSelectionEnabled = false;
            }

            e.Handled = handled;
        }

        enum SelectionDirection
        {
            None,
            Up,
            Down,
        }

        #endregion
    }

    public class BidirectionalIncrementalLoader
    {
        private readonly ChatHistoryView _listView;
        private ScrollViewer _scrollViewer;
        private ItemsStackPanel _itemsPanel;

        private int _activeLoadOperations;
        private CancellationTokenSource _cts;

        private bool _isMonitoring;
        private bool _isInitialized;

        private readonly double _baseTopTriggerThreshold;
        private readonly double _baseBottomTriggerThreshold;
        private readonly double _minThresholdMultiplier;
        private readonly double _maxThresholdMultiplier;
        private readonly TimeSpan _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private int _consecutiveSizeChangedChecks;
        private const int MaxConsecutiveSizeChangedChecks = 20;
        private double _lastPanelHeight;

        private DialogViewModel _viewModel;

        public BidirectionalIncrementalLoader(
            ChatHistoryView listView,
            double baseTopTriggerThreshold = 800.0,
            double baseBottomTriggerThreshold = 800.0,
            double minThresholdMultiplier = 0.5,
            double maxThresholdMultiplier = 2.0,
            TimeSpan? checkInterval = null)
        {
            _listView = listView ?? throw new ArgumentNullException(nameof(listView));

            _baseTopTriggerThreshold = baseTopTriggerThreshold;
            _baseBottomTriggerThreshold = baseBottomTriggerThreshold;
            _minThresholdMultiplier = minThresholdMultiplier;
            _maxThresholdMultiplier = maxThresholdMultiplier;
            _checkInterval = checkInterval ?? TimeSpan.FromMilliseconds(150);

            _listView.Loaded += OnListViewLoaded;
            _listView.Unloaded += OnListViewUnloaded;
        }

        public void Initialize(DialogViewModel viewModel)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (_viewModel != null)
            {
                _viewModel.MessagesLoaded -= OnMessagesLoaded;
            }

            _isInitialized = false;
            _activeLoadOperations = 0;
            _consecutiveSizeChangedChecks = 0;

            _viewModel = viewModel;
            _viewModel.MessagesLoaded += OnMessagesLoaded;
        }

        private void OnMessagesLoaded(object sender, MessagesLoadedEventArgs e)
        {
            if (e.Direction == PanelScrollingDirection.None)
            {
                _isInitialized = true;

                if (_isMonitoring && _scrollViewer != null)
                {
                    CheckNonScrollableState();
                }
            }
            else if (e.Direction == PanelScrollingDirection.Backward)
            {
                //_listView.SetScrollingMode(ItemsUpdatingScrollMode.KeepItemsInView, force: false);
            }
            else if (e.Direction == PanelScrollingDirection.Forward)
            {
                //_listView.SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, force: false);
            }
        }

        private void OnListViewLoaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = _listView.ScrollingHost;
            if (_scrollViewer == null)
            {
                return;
            }

            _itemsPanel = _listView.ItemsPanelRoot as ItemsStackPanel;
            if (_itemsPanel == null)
            {
                return;
            }

            StartMonitoring();
        }

        private void OnListViewUnloaded(object sender, RoutedEventArgs e)
        {
            StopMonitoring();
        }

        private void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _scrollViewer.ViewChanged += OnViewChanged;
            _itemsPanel.SizeChanged += OnItemsPanelSizeChanged;
            _lastPanelHeight = _itemsPanel.ActualHeight;

            if (_isInitialized)
            {
                CheckNonScrollableState();
            }
        }

        private void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _scrollViewer.ViewChanged -= OnViewChanged;
            _itemsPanel.SizeChanged -= OnItemsPanelSizeChanged;
            _consecutiveSizeChangedChecks = 0;
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!_isInitialized || _viewModel == null)
                return;

            _consecutiveSizeChangedChecks = 0;

            if (!e.IsIntermediate)
            {
                _lastCheckTime = DateTime.MinValue;
                CheckAndLoad();
            }
            else
            {
                var now = DateTime.UtcNow;
                if (now - _lastCheckTime >= _checkInterval)
                {
                    _lastCheckTime = now;
                    CheckAndLoad();
                }
            }
        }

        private void OnItemsPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized || _viewModel == null)
                return;

            if (Math.Abs(e.NewSize.Height - _lastPanelHeight) < 0.1)
                return;

            _lastPanelHeight = e.NewSize.Height;
            CheckNonScrollableState();
        }

        private void CheckNonScrollableState()
        {
            if (_activeLoadOperations > 0 || _viewModel == null)
            {
                _consecutiveSizeChangedChecks = 0;
                return;
            }

            if (!_viewModel.HasMoreItemsAtTop && !_viewModel.HasMoreItemsAtBottom)
            {
                _consecutiveSizeChangedChecks = 0;
                return;
            }

            bool isScrollable = _scrollViewer.ScrollableHeight > 0;
            if (!isScrollable)
            {
                _consecutiveSizeChangedChecks++;

                if (_consecutiveSizeChangedChecks > MaxConsecutiveSizeChangedChecks)
                {
                    _consecutiveSizeChangedChecks = 0;
                    return;
                }

                if (_viewModel.HasMoreItemsAtTop)
                {
                    LoadItems(PanelScrollingDirection.Backward);
                }
                else if (_viewModel.HasMoreItemsAtBottom)
                {
                    LoadItems(PanelScrollingDirection.Forward);
                }
            }
            else
            {
                _consecutiveSizeChangedChecks = 0;
                CheckAndLoad();
            }
        }

        private void CheckAndLoad()
        {
            if (!_isInitialized || _scrollViewer == null || _itemsPanel == null || _viewModel == null)
                return;

            var verticalOffset = _scrollViewer.VerticalOffset;
            var viewportHeight = _scrollViewer.ViewportHeight;
            var scrollableHeight = _scrollViewer.ScrollableHeight;

            if (scrollableHeight == 0)
                return;

            var (topThreshold, bottomThreshold) = CalculateDynamicThresholds(
                verticalOffset,
                viewportHeight,
                scrollableHeight
            );

            if (_viewModel.HasMoreItemsAtTop && verticalOffset < topThreshold)
            {
                LoadItems(PanelScrollingDirection.Backward);
            }

            var distanceFromBottom = scrollableHeight - verticalOffset;
            if (_viewModel.HasMoreItemsAtBottom && distanceFromBottom < bottomThreshold)
            {
                LoadItems(PanelScrollingDirection.Forward);
            }
        }

        private (double topThreshold, double bottomThreshold) CalculateDynamicThresholds(
            double verticalOffset,
            double viewportHeight,
            double scrollableHeight)
        {
            double relativePosition = scrollableHeight > 0
                ? verticalOffset / scrollableHeight
                : 0.5;

            double totalContentHeight = scrollableHeight + viewportHeight;

            double contentRatio = totalContentHeight / viewportHeight;
            double sizeMultiplier = Math.Clamp(
                contentRatio / 5.0,
                _minThresholdMultiplier,
                _maxThresholdMultiplier
            );

            double topPositionMultiplier = 1.0 + (1.0 - relativePosition) * 0.5;
            double bottomPositionMultiplier = 1.0 + relativePosition * 0.5;

            double topThreshold = Math.Min(
                _baseTopTriggerThreshold * sizeMultiplier * topPositionMultiplier,
                viewportHeight * 2.0
            );

            double bottomThreshold = Math.Min(
                _baseBottomTriggerThreshold * sizeMultiplier * bottomPositionMultiplier,
                viewportHeight * 2.0
            );

            return (topThreshold, bottomThreshold);
        }

        private void LoadItems(PanelScrollingDirection direction)
        {
            if (_viewModel == null)
                return;

            Interlocked.Increment(ref _activeLoadOperations);

            //if (direction == PanelScrollingDirection.Backward)
            //{
            //    _listView.SetScrollingMode(ItemsUpdatingScrollMode.KeepItemsInView, force: true);
            //}
            //else if (direction == PanelScrollingDirection.Forward)
            //{
            //    _listView.SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, force: true);
            //}

            _ = LoadItemsInternalAsync(direction);
        }

        private async Task LoadItemsInternalAsync(PanelScrollingDirection direction)
        {
            var ct = _cts.Token;

            try
            {
                if (ct.IsCancellationRequested || _viewModel == null)
                    return;

                await _viewModel.LoadNextSliceAsync(direction);
                await _scrollViewer.UpdateLayoutAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation or view model swap
            }
            catch (Exception)
            {
                // View model should handle its own errors
                // We just track the operation completion
            }
            finally
            {
                Interlocked.Decrement(ref _activeLoadOperations);

                if (!ct.IsCancellationRequested && _viewModel != null)
                {
                    CheckContinueLoading(direction);
                }
            }
        }

        private void CheckContinueLoading(PanelScrollingDirection direction)
        {
            var verticalOffset = _scrollViewer.VerticalOffset;
            var viewportHeight = _scrollViewer.ViewportHeight;
            var scrollableHeight = _scrollViewer.ScrollableHeight;

            if (scrollableHeight == 0)
            {
                return;
            }

            var (topThreshold, bottomThreshold) = CalculateDynamicThresholds(
                verticalOffset,
                viewportHeight,
                scrollableHeight
            );

            if (direction == PanelScrollingDirection.Backward)
            {
                if (_viewModel.HasMoreItemsAtTop && verticalOffset < topThreshold)
                {
                    LoadItems(PanelScrollingDirection.Backward);
                }
            }
            else if (direction == PanelScrollingDirection.Forward)
            {
                var distanceFromBottom = scrollableHeight - verticalOffset;
                if (_viewModel.HasMoreItemsAtBottom && distanceFromBottom < bottomThreshold)
                {
                    LoadItems(PanelScrollingDirection.Forward);
                }
            }
        }
    }
}
