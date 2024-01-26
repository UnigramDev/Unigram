using System;
using System.Collections.Specialized;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Stories
{
    public sealed partial class StoriesStrip : UserControlEx
    {
        public StoryListViewModel ViewModel => DataContext as StoryListViewModel;

        public StoriesStrip()
        {
            InitializeComponent();

            _scrollDebouncer = new EventDebouncer<NotifyCollectionChangedEventArgs>(100,
                handler => ViewModel.Items.CollectionChanged += new NotifyCollectionChangedEventHandler(handler),
                handler => ViewModel.Items.CollectionChanged -= new NotifyCollectionChangedEventHandler(handler));

            _scrollTracker = new DispatcherTimer();
            _scrollTracker.Interval = TimeSpan.FromMilliseconds(33);
            _scrollTracker.Tick += OnTick;

            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            _scrollDebouncer.Invoked += OnCollectionChanged;
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            _scrollDebouncer.Invoked -= OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_collapsed || ScrollingHost.Items.Count > 0)
            {
                UpdateIndexes();
            }
            else
            {
                Collapse();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _scrollDebouncer.Invoke();
        }

        private int _first = 0;
        private int _last = -1;

        private void UpdateIndexes()
        {
            if (ViewModel.Items.Count > 0)
            {
                if (ViewModel.Items[0].IsMyStory && ViewModel.Items.Count > 1)
                {
                    _first = 1;
                }
                else
                {
                    _first = 0;
                }

                _last = Math.Min(_first + 2, Math.Max(_first, ViewModel.Items.Count - 1));
            }
            else
            {
                _first = 0;
                _last = -1;
            }

            if (_progress != null)
            {
                _progress.InsertScalar("First", _first);
                _progress.InsertScalar("Last", _last);
                _progress.InsertScalar("Count", _last - _first + 1);

                ForEach(_progress, _progressAnimation);
            }

            var count = _last - _first + 1;
            if (count > 0 && _collapsed)
            {
                Show.Width = count * 12 + 12 + 8;
                Show.Visibility = Visibility.Visible;
            }
            else
            {
                Show.Visibility = Visibility.Collapsed;
            }

            ScrollingHost.IsHitTestVisible = !_collapsed;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var i = args.ItemIndex;

            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ActiveStoriesCell cell && args.Item is ActiveStoriesViewModel item)
            {
                UpdateIndexes();

                cell.Update(item);
                cell.Update(args.ItemContainer, args.ItemIndex, _first, _last, _progress, _progressAnimation);

                AutomationProperties.SetName(args.ItemContainer, cell.GetAutomationName());
                Canvas.SetZIndex(args.ItemContainer, i >= _first && i <= _last ? 5 - i : -i);
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var activeStories = ScrollingHost.ItemFromContainer(sender) as ActiveStoriesViewModel;

            if (activeStories == null || activeStories.IsMyStory || (activeStories.ClientService.TryGetUser(activeStories.Chat, out User user) && user.IsSupport))
            {
                return;
            }

            var muted = ViewModel.Settings.Notifications.GetMuteStories(activeStories.Chat);
            var archived = activeStories.List is StoryListArchive;

            var flyout = new MenuFlyout();

            if (activeStories.Chat.Type is ChatTypePrivate)
            {
                flyout.CreateFlyoutItem(ViewModel.SendMessage, activeStories, Strings.SendMessage, Icons.ChatEmpty);
                flyout.CreateFlyoutItem(ViewModel.OpenProfile, activeStories, Strings.OpenProfile, Icons.Person);
            }
            else if (activeStories.Chat.Type is ChatTypeSupergroup typeSupergroup && typeSupergroup.IsChannel)
            {
                flyout.CreateFlyoutItem(ViewModel.OpenProfile, activeStories, Strings.OpenChannel2, Icons.Megaphone);
            }

            if (activeStories.Chat.Type is ChatTypePrivate && !activeStories.IsMyStory)
            {
                flyout.CreateFlyoutItem(ViewModel.MuteProfile, activeStories, muted ? Strings.NotificationsStoryUnmute2 : Strings.NotificationsStoryMute2, muted ? Icons.Alert : Icons.AlertOff);
            }

            if (archived)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowProfile, activeStories, Strings.UnarchiveStories, Icons.Unarchive);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.HideProfile, activeStories, Strings.ArchivePeerStories, Icons.Archive);
            }

            flyout.ShowAt(element, args);
        }

        public void ForEach(CompositionPropertySet tracker, ExpressionAnimation expression)
        {
            var panel = ScrollingHost.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null || panel.FirstVisibleIndex != 0)
            {
                return;
            }

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ScrollingHost.ContainerFromIndex(i) as SelectorItem;
                if (container != null)
                {
                    if (container.ContentTemplateRoot is ActiveStoriesCell cell)
                    {
                        if (i >= panel.FirstVisibleIndex && i <= panel.LastVisibleIndex)
                        {
                            cell.Update(container, i, _first, _last, tracker, expression);
                            Canvas.SetZIndex(container, i >= _first && i <= _last ? 5 - i : -i);
                        }
                        else
                        {
                            cell.Disconnect(container);
                        }
                    }
                }
            }
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            Expand();
        }

        private void ScrollingHost_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ActiveStoriesViewModel activeStories)
            {
                if (_collapsed)
                {
                    Expand();
                }
                else
                {
                    var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var transform = container.TransformToVisual(null);
                    var point = transform.TransformPoint(new Point());

                    var origin = new Rect(point.X + 8 + 4, point.Y + 12 + 4, 40, 40);

                    ViewModel.OpenStory(activeStories, origin, GetOrigin);
                }
            }

            //UpdateIndexes();
            //ForEach(_progress, _progressAnimation);

            //ScrollingHost.ItemsSource = null;
            //ScrollingHost.ItemsSource = ViewModel.Items;
        }

        private Rect GetOrigin(ActiveStoriesViewModel activeStories)
        {
            var container = ScrollingHost.ContainerFromItem(activeStories) as SelectorItem;
            if (container != null)
            {
                var transform = container.TransformToVisual(null);
                var point = transform.TransformPoint(new Point());

                return new Rect(point.X + 8 + 4, point.Y + 12 + 4, 40, 40);
            }

            return Rect.Empty;
        }

        public void ScrollToTop()
        {
            ScrollingHost.ScrollToTop();
        }

        public event EventHandler<StoryEventArgs> ItemClick;

        #region Manipulation

        private CompositionPropertySet _progress;
        private ExpressionAnimation _progressAnimation;

        private DispatcherTimer _scrollTracker;
        private EventDebouncer<NotifyCollectionChangedEventArgs> _scrollDebouncer;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_controlledList != null && _scrollViewer == null)
            {
                SetControlledList(_controlledList);
            }
        }

        public bool IsCollapsed => _collapsed;

        private ScrollViewer _scrollViewer;

        private FrameworkElement _chatTabs;
        public FrameworkElement ChatTabs
        {
            get => _chatTabs;
            set
            {
                _chatTabs = value;

                if (_chatTabs != null && _progress != null)
                {
                    SetControlledList(_controlledList);
                }
            }
        }

        public FrameworkElement TitleBarrr { get; set; }
        public FrameworkElement Header { get; set; }

        private bool _tabsLeftCollapsed = true;
        public bool TabsLeftCollapsed
        {
            get => _tabsLeftCollapsed;
            set
            {
                _tabsLeftCollapsed = value;
                UpdatePadding();
            }
        }

        private float _systemOverlayLeftInset;
        public float SystemOverlayLeftInset
        {
            get => _systemOverlayLeftInset;
            set
            {
                _systemOverlayLeftInset = value;
                UpdatePadding();
            }
        }

        private void UpdatePadding()
        {
            _progress?.InsertBoolean("RightToLeft", _systemOverlayLeftInset > 0);
            _progress?.InsertScalar("Padding", _systemOverlayLeftInset > 0 ? _systemOverlayLeftInset : _tabsLeftCollapsed ? 32 : 0);
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                _progress?.InsertBoolean("Visible", value);
            }
        }

        public bool TabsTopCollapsed { get; set; } = true;

        private ListView _controlledList;
        public ListView ControlledList
        {
            get => _controlledList;
            set => SetControlledList(_controlledList = value);
        }

        public double TopPadding
        {
            get
            {
                var title = 40;
                var search = 32;
                var padding = 4 + (TabsTopCollapsed || !TabsLeftCollapsed ? 0 : 36);
                //var padding = 8 + (ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 32);
                //var padding = ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 40;

                return title + search + padding;
            }
        }

        public double GetTopPadding(bool collapsed)
        {
            var title = 40;
            var search = 32;
            var padding = 4 + (collapsed /*|| !TabsLeftCollapsed*/ ? 0 : 36);
            //var padding = 8 + (ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 32);
            //var padding = ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 40;

            return title + search + padding;
        }

        private void SetControlledList(FrameworkElement value)
        {
            var scrollViewer = value as ScrollViewer;
            if (scrollViewer == null && value != null)
            {
                if (value.IsLoaded)
                {
                    scrollViewer = ControlledList.GetScrollViewer();
                }
                else
                {
                    value.Loaded += OnLoaded;
                    return;
                }
            }

            if (_scrollViewer != null)
            {
                _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                _scrollViewer.ViewChanging -= Scroller_ViewChanging;

                _scrollViewer.DirectManipulationStarted -= Scroller_DirectManipulationStarted;
                _scrollViewer.DirectManipulationCompleted -= Scroller_DirectManipulationCompleted;

                ControlledList.SizeChanged -= ControlledList_SizeChanged;
            }

            _scrollViewer = scrollViewer;

            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanging += Scroller_ViewChanging;

            _scrollViewer.DirectManipulationStarted += Scroller_DirectManipulationStarted;
            _scrollViewer.DirectManipulationCompleted += Scroller_DirectManipulationCompleted;

            _collapsed = true;

            ControlledList.SizeChanged += ControlledList_SizeChanged;
            ControlledList.Margin = new Thickness(0, TopPadding, 0, 0);
            ControlledList.Padding = new Thickness();

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
            var compositor = properties.Compositor;

            var clip = compositor.CreateInsetClip();

            _progressAnimation = compositor.CreateExpressionAnimation($"Clamp((1 - (((This.Collapsed ? 88 : 0) + -(interactionTracker.Translation.Y > -1 && interactionTracker.Translation.Y < 1 ? 0 : interactionTracker.Translation.Y)) / 88)) * (This.Collapsed ? 0.5 : 1), 0, 1)");
            _progressAnimation.SetReferenceParameter("interactionTracker", properties);
            _progressAnimation.Properties.InsertBoolean("Collapsed", true);

            _progress = compositor.CreatePropertySet();
            _progress.InsertBoolean("Visible", _isVisible);
            _progress.InsertBoolean("RightToLeft", false);
            _progress.InsertScalar("Padding", _systemOverlayLeftInset > 0 ? _systemOverlayLeftInset - (_tabsLeftCollapsed ? 0 : 72) : _tabsLeftCollapsed ? 32 : 0);
            _progress.InsertScalar("First", _first);
            _progress.InsertScalar("Last", _last);
            _progress.InsertScalar("Count", _last - _first + 1);
            _progress.InsertScalar("Progress", 0);
            _progress.StartAnimation("Progress", _progressAnimation);

            //            m_progress?.InsertScalar("Padding", _tabsLeftCollapsed ? 48 : 0);


            ForEach(_progress, _progressAnimation);

            var titleVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                "_.RightToLeft ? 0 : _.Visible && _.Count > 0 ? (24 + (12 * _.Count)) * (1 - _.Progress) : 0");

            var storiesVisualOffsetAnimationX = compositor.CreateExpressionAnimation(
                "(_.Padding - _.First * 12) * (1 - _.Progress)");

            var storiesVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                "-22 + (48 * _.Progress)");

            var headerVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                "84 * _.Progress");

            titleVisualOffsetAnimation.SetReferenceParameter("_", _progress);
            storiesVisualOffsetAnimationX.SetReferenceParameter("_", _progress);
            storiesVisualOffsetAnimation.SetReferenceParameter("_", _progress);
            headerVisualOffsetAnimation.SetReferenceParameter("_", _progress);

            var titleVisual = ElementComposition.GetElementVisual(TitleBarrr);
            var storiesVisual = ElementComposition.GetElementVisual(this);
            var headerVisual = ElementComposition.GetElementVisual(Header);

            storiesVisual.Clip = clip;

            titleVisual.Properties.InsertVector3("Translation", Vector3.Zero);
            storiesVisual.Properties.InsertVector3("Translation", Vector3.Zero);
            headerVisual.Properties.InsertVector3("Translation", Vector3.Zero);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleBarrr, true);
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Header, true);

            titleVisual.StartAnimation("Translation.X", titleVisualOffsetAnimation);
            storiesVisual.StartAnimation("Translation.X", storiesVisualOffsetAnimationX);
            clip.StartAnimation("RightInset", storiesVisualOffsetAnimationX);
            storiesVisual.StartAnimation("Translation.Y", storiesVisualOffsetAnimation);
            headerVisual.StartAnimation("Translation.Y", headerVisualOffsetAnimation);

            if (ChatTabs != null)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(ChatTabs, true);

                var tabsVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                    "-80 + 84 * _.Progress");
                tabsVisualOffsetAnimation.SetReferenceParameter("_", _progress);

                var tabsVisual = ElementComposition.GetElementVisual(ChatTabs);
                tabsVisual.Properties.InsertVector3("Translation", Vector3.Zero);
                tabsVisual.StartAnimation("Translation.Y", tabsVisualOffsetAnimation);
            }
        }

        private void ControlledList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMinHeight();
        }

        private void UpdateMinHeight()
        {
            if (ControlledList.ItemsPanelRoot == null)
            {
                return;
            }

            if (_collapsed)
            {
                ControlledList.ItemsPanelRoot.MinHeight = 0;
            }
            else
            {
                ControlledList.ItemsPanelRoot.MinHeight = ControlledList.ActualHeight;
            }
        }

        private bool _directManipulation;

        private void Scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (e.IsInertial || !e.FinalView.VerticalOffset.AlmostEqualsToZero(0.5))
            {
                _scrollTracker.Stop();
            }
            else if (_collapsed && _directManipulation && !_scrollTracker.IsEnabled)
            {
                _scrollTracker.Start();
            }
        }

        private void OnTick(object sender, object e)
        {
            var test = ControlledList.Header switch
            {
                FrameworkElement header => header.Visibility == Visibility.Visible ? header : ControlledList.ItemsPanelRoot,
                _ => ControlledList.ItemsPanelRoot
            };

            if (test != null && _collapsed)
            {
                var transform = test.TransformToVisual(ControlledList);
                var point = transform.TransformPoint(new Point()).ToVector2();

                Logger.Info(point.Y);

                if (point.Y > 48 && _collapsed && _scrollViewer.VerticalOffset.AlmostEqualsToZero(0.5))
                {
                    _scrollTracker.Stop();
                    Expand(true, point.Y);
                }
            }
            else
            {
                _scrollTracker.Stop();
            }
        }

        private void Batch_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= Batch_Completed;
            }

            _progress.StartAnimation("Progress", _progressAnimation);
        }

        private void Scroller_DirectManipulationStarted(object sender, object e)
        {
            _directManipulation = true;

            UpdateIndexes();

            if (_collapsed && _scrollViewer.VerticalOffset.AlmostEqualsToZero(0.5))
            {
                _scrollTracker.Start();
            }
        }

        private void Scroller_DirectManipulationCompleted(object sender, object e)
        {
            _directManipulation = false;
            _scrollTracker.Stop();
        }

        private bool _collapsed;

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_collapsed && _scrollViewer.VerticalOffset.AlmostEqualsToZero() && !e.IsIntermediate)
            {
                //_collapsed = false;

                //ChatsList.Padding = new Thickness(0, 88, 0, 0);
                //ComposeButton.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                //m_progressAnimation.Properties.InsertBoolean("Collapsed", false);
                //scrollViewer.ChangeView(null, 88, null, true);
                //scrollViewer.SetVerticalPadding(88);
            }
            else if (_scrollViewer.VerticalOffset > 88 && !_collapsed)
            {
                Collapse(false);
            }
            else if (_scrollViewer.VerticalOffset < 88 && !_collapsed)
            {
                if (e.IsIntermediate)
                {

                }
                else if (_scrollViewer.VerticalOffset >= 40)
                {
                    _scrollViewer.ChangeView(null, 88, null, false);
                }
                else
                {
                    _scrollViewer.ChangeView(null, 0, null, false);
                }

                ScrollToTop();
            }
            else if (_scrollViewer.VerticalOffset.AlmostEquals(88, 0.5) && !_collapsed && !e.IsIntermediate)
            {
                Collapse(false);
            }
        }

        public void Toggle()
        {
            if (_collapsed)
            {
                Expand();
            }
            else
            {
                Collapse();
            }
        }

        public void Collapse(bool animated = true)
        {
            if (_collapsed || _scrollViewer == null)
            {
                return;
            }

            Logger.Info();

            _collapsed = true;
            Collapsing?.Invoke(this, EventArgs.Empty);

            ControlledList.Padding = new Thickness(0, 0, 0, 0);
            _progressAnimation.Properties.InsertBoolean("Collapsed", true);
            _scrollViewer.SetVerticalPadding(0);

            UpdateMinHeight();
            UpdateIndexes();

            if (animated)
            {
                var batch = _progress.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += Batch_Completed;

                var animation = _progress.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);
                animation.Duration = Constants.FastAnimation;

                var translation = _progress.Compositor.CreateScalarKeyFrameAnimation();
                translation.InsertKeyFrame(0, 88);
                translation.InsertKeyFrame(1, 0);
                translation.Duration = Constants.FastAnimation;

                ElementCompositionPreview.SetIsTranslationEnabled(ControlledList, true);
                var boh = ElementComposition.GetElementVisual(ControlledList);

                _progress.StartAnimation("Progress", animation);
                boh.StartAnimation("Translation.Y", translation);
                batch.End();
            }
            else if (_directManipulation)
            {
                _scrollViewer.ChangeView(null, _scrollViewer.VerticalOffset - 88, null, true);
            }
            else
            {
                _scrollViewer.CancelDirectManipulations();
                _scrollViewer.ChangeView(null, 0, null, true);
            }
        }

        public event EventHandler Expanding;
        public event EventHandler Collapsing;

        public void Expand(bool animated = true, float offset = 0)
        {
            if (!_collapsed || _scrollViewer == null || ScrollingHost.Items.Count < 1)
            {
                return;
            }

            Logger.Info();

            _collapsed = false;
            Expanding?.Invoke(this, EventArgs.Empty);

            ControlledList.Padding = new Thickness(0, 88, 0, 0);
            _progressAnimation.Properties.InsertBoolean("Collapsed", false);
            _scrollViewer.SetVerticalPadding(88);

            UpdateMinHeight();
            UpdateIndexes();

            if (animated)
            {
                var batch = _progress.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += Batch_Completed;

                var animation = _progress.Compositor.CreateScalarKeyFrameAnimation();
                if (offset == 0)
                {
                    animation.InsertKeyFrame(0, 0);
                }
                animation.InsertKeyFrame(1, 1);
                animation.Duration = Constants.FastAnimation;

                var translation = _progress.Compositor.CreateScalarKeyFrameAnimation();
                translation.InsertKeyFrame(0, -(88 - offset));
                translation.InsertKeyFrame(1, 0);
                translation.Duration = Constants.FastAnimation;

                ElementCompositionPreview.SetIsTranslationEnabled(ControlledList, true);
                var boh = ElementComposition.GetElementVisual(ControlledList);

                _progress.StartAnimation("Progress", animation);
                boh.StartAnimation("Translation.Y", translation);
                batch.End();

                _scrollViewer.ChangeView(null, 0, null, true);
            }
        }



        #endregion
    }
}
