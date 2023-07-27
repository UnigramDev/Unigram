using LinqToVisualTree;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Stories
{
    public sealed partial class StoriesStrip : UserControl
    {
        public StoryListViewModel ViewModel => DataContext as StoryListViewModel;

        public StoriesStrip()
        {
            InitializeComponent();

            _scrollTracker = new DispatcherTimer();
            _scrollTracker.Interval = TimeSpan.FromMilliseconds(33);
            _scrollTracker.Tick += OnTick;

            ScrollingHost.ItemContainerGenerator.ItemsChanged += OnItemsChanged;
        }

        private void OnItemsChanged(object sender, ItemsChangedEventArgs e)
        {
            if (ScrollingHost.Items.Count > 0)
            {
                UpdateIndexes();
            }
            else
            {
                Collapse();
            }
        }

        private int _first = 0;
        private int _last = -1;

        private void UpdateIndexes()
        {
            if (ViewModel.Items.Count > 0)
            {
                if (ViewModel.Items[0].IsMyStory)
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

                Canvas.SetZIndex(args.ItemContainer, i >= _first && i <= _last ? 5 - i : -i);
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var activeStories = ScrollingHost.ItemFromContainer(sender) as ActiveStoriesViewModel;

            if (activeStories == null || activeStories.IsMyStory)
            {
                return;
            }

            var muted = ViewModel.Settings.Notifications.GetMuteStories(activeStories.Chat);
            var archived = activeStories.List is StoryListArchive;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.SendMessage, activeStories, Strings.SendMessage, Icons.ChatEmpty);
            flyout.CreateFlyoutItem(ViewModel.OpenProfile, activeStories, Strings.OpenProfile, Icons.Person);
            flyout.CreateFlyoutItem(ViewModel.MuteProfile, activeStories, muted ? Strings.NotificationsStoryUnmute2 : Strings.NotificationsStoryMute2, muted ? Icons.Alert : Icons.AlertOff);

            if (archived)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowProfile, activeStories, Strings.UnarchiveStories, Icons.Unarchive);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.HideProfile, activeStories, Strings.ArchivePeerStories, Icons.Archive);
            }

            args.ShowAt(flyout, element);
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

        private void StoryCell_Click(object sender, StoryEventArgs e)
        {
            if (sender is FrameworkElement photo)
            {
                if (_collapsed)
                {
                    Expand();
                }
                else
                {
                    var transform = photo.TransformToVisual(Window.Current.Content);
                    var point = transform.TransformPoint(new Point());

                    var origin = new Rect(point.X + 12 + 4, point.Y + 12 + 4, 40, 40);

                    ViewModel.OpenStory(e.ActiveStories, origin, GetOrigin);
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
                var transform = container.TransformToVisual(Window.Current.Content);
                var point = transform.TransformPoint(new Point());

                return new Rect(point.X + 12 + 4, point.Y + 12 + 4, 40, 40);
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_controlledList != null && _scrollViewer == null)
            {
                SetControlledList(_controlledList);
            }
        }

        public bool IsCollapsed => _collapsed;

        private ScrollViewer _scrollViewer;

        public FrameworkElement ChatTabs { get; set; }
        public FrameworkElement TitleBarrr { get; set; }
        public FrameworkElement Header { get; set; }

        private bool _tabsLeftCollapsed = true;
        public bool TabsLeftCollapsed
        {
            get => _tabsLeftCollapsed;
            set
            {
                _tabsLeftCollapsed = value;
                _progress?.InsertScalar("Padding", value ? 32 : 0);
            }
        }

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
                var padding = 4 + (ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 36);
                //var padding = 8 + (ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 32);
                //var padding = ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || !TabsLeftCollapsed ? 0 : 40;

                return title + search + padding;
            }
        }

        private void SetControlledList(FrameworkElement value)
        {
            var scrollViewer = value as ScrollViewer;
            if (scrollViewer == null && value != null)
            {
                if (value.IsLoaded)
                {
                    scrollViewer = ControlledList.Descendants<ScrollViewer>().FirstOrDefault();
                }
                else
                {
                    value.Loaded += OnLoaded;
                    return;
                }
            }

            _scrollViewer = scrollViewer;

            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanging += Scroller_ViewChanging;

            _scrollViewer.DirectManipulationStarted += Scroller_DirectManipulationStarted;
            _scrollViewer.DirectManipulationCompleted += Scroller_DirectManipulationCompleted;

            _collapsed = true;

            ControlledList.Margin = new Thickness(0, TopPadding, 0, 0);
            ControlledList.Padding = new Thickness();

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
            var compositor = properties.Compositor;

            _progressAnimation = compositor.CreateExpressionAnimation($"Clamp((1 - (((This.Collapsed ? 88 : 0) + -(interactionTracker.Translation.Y > -1 && interactionTracker.Translation.Y < 1 ? 0 : interactionTracker.Translation.Y)) / 88)) * (This.Collapsed ? 0.5 : 1), 0, 1)");
            _progressAnimation.SetReferenceParameter("interactionTracker", properties);
            _progressAnimation.Properties.InsertBoolean("Collapsed", true);

            _progress = compositor.CreatePropertySet();
            _progress.InsertScalar("Padding", TabsLeftCollapsed ? 32 : 0);
            _progress.InsertScalar("First", _first);
            _progress.InsertScalar("Last", _last);
            _progress.InsertScalar("Count", _last - _first + 1);
            _progress.InsertScalar("Progress", 0);
            _progress.StartAnimation("Progress", _progressAnimation);

            //            m_progress?.InsertScalar("Padding", _tabsLeftCollapsed ? 48 : 0);


            ForEach(_progress, _progressAnimation);

            var titleVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                "_.Count > 0 ? (24 + (12 * _.Count)) * (1 - _.Progress) : 0");

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

            var titleVisual = ElementCompositionPreview.GetElementVisual(TitleBarrr);
            var storiesVisual = ElementCompositionPreview.GetElementVisual(this);
            var headerVisual = ElementCompositionPreview.GetElementVisual(Header);

            titleVisual.Properties.InsertVector3("Translation", Vector3.Zero);
            storiesVisual.Properties.InsertVector3("Translation", Vector3.Zero);
            headerVisual.Properties.InsertVector3("Translation", Vector3.Zero);

            ElementCompositionPreview.SetIsTranslationEnabled(TitleBarrr, true);
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Header, true);

            titleVisual.StartAnimation("Translation.X", titleVisualOffsetAnimation);
            storiesVisual.StartAnimation("Translation.X", storiesVisualOffsetAnimationX);
            storiesVisual.StartAnimation("Translation.Y", storiesVisualOffsetAnimation);
            headerVisual.StartAnimation("Translation.Y", headerVisualOffsetAnimation);

            if (ChatTabs != null)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(ChatTabs, true);

                var tabsVisualOffsetAnimation = compositor.CreateExpressionAnimation(
                    "-80 + 84 * _.Progress");
                tabsVisualOffsetAnimation.SetReferenceParameter("_", _progress);

                var tabsVisual = ElementCompositionPreview.GetElementVisual(ChatTabs);
                tabsVisual.Properties.InsertVector3("Translation", Vector3.Zero);
                tabsVisual.StartAnimation("Translation.Y", tabsVisualOffsetAnimation);
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

            _collapsed = true;
            Collapsing?.Invoke(this, EventArgs.Empty);

            ControlledList.Padding = new Thickness(0, 0, 0, 0);
            _progressAnimation.Properties.InsertBoolean("Collapsed", true);
            _scrollViewer.SetVerticalPadding(0);

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
                var boh = ElementCompositionPreview.GetElementVisual(ControlledList);

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

            _collapsed = false;
            Expanding?.Invoke(this, EventArgs.Empty);

            ControlledList.Padding = new Thickness(0, 88, 0, 0);
            _progressAnimation.Properties.InsertBoolean("Collapsed", false);
            _scrollViewer.SetVerticalPadding(88);

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
                var boh = ElementCompositionPreview.GetElementVisual(ControlledList);

                _progress.StartAnimation("Progress", animation);
                boh.StartAnimation("Translation.Y", translation);
                batch.End();

                _scrollViewer.ChangeView(null, 0, null, true);
            }
        }



        #endregion
    }
}
