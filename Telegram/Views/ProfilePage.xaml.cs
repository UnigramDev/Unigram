//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Stories;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Profile;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class ProfilePage : HostedPage, IProfileDelegate, INavigablePage
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        private readonly DispatcherTimer _dateHeaderTimer;
        private Visual _dateHeaderPanel;
        private bool _dateHeaderCollapsed = true;

        public ProfilePage()
        {
            InitializeComponent();

            _dateHeaderTimer = new DispatcherTimer();
            _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
            _dateHeaderTimer.Tick += (s, args) =>
            {
                _dateHeaderTimer.Stop();
                ShowHideDateHeader(false, true);
            };

            ScrollingHost.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(ScrollingHost);
            }
        }

        public override HostedPagePositionBase GetPosition()
        {
            ViewModel.Delegate = null;
            return new HostedPageListViewPosition(DataContext, ScrollingHost.VerticalOffset, string.Empty);
        }

        public override void SetPosition(HostedPagePositionBase position)
        {
            if (position is HostedPageListViewPosition listViewPosition)
            {
                DataContext = listViewPosition.DataContext;
                ViewModel.Delegate = this;

                void handler(object sender, RoutedEventArgs e)
                {
                    _hasBeenScrolled = false;
                    RootGrid.Unsnap();

                    ScrollingHost.Loaded -= handler;
                    ScrollingHost.TryChangeView(null, listViewPosition.ScrollPosition, null, true);
                }

                ScrollingHost.Loaded += handler;
            }
        }

        private void InitializeScrolling()
        {
            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var visual = ElementComposition.GetElementVisual(HeaderPanel);
            var border = ElementComposition.GetElementVisual(CardBackground);
            var menu = ElementComposition.GetElementVisual(MenuRoot);
            var media = ElementComposition.GetElementVisual(MediaFrame);
            media.Clip = media.Compositor.CreateInsetClip();

            ElementCompositionPreview.SetIsTranslationEnabled(HeaderPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(BackButton, true);

            var translation = visual.Compositor.CreateExpressionAnimation(ViewModel.IsSavedMessages ? "-scrollViewer.Translation.Y" :
                $"_.ActualHeight > 16 ? scrollViewer.Translation.Y > -(_.ActualHeight + 8) ? 0 : -scrollViewer.Translation.Y - (_.ActualHeight + 8) : -scrollViewer.Translation.Y");
            translation.SetReferenceParameter("scrollViewer", properties);
            translation.SetReferenceParameter("_", ProfileHeader.Properties);

            var clip = visual.Compositor.CreateExpressionAnimation(
                "-scrollViewer.Translation.Y + 4");
            clip.SetReferenceParameter("scrollViewer", properties);
            clip.SetReferenceParameter("properties", ProfileHeader.Properties);

            //var fadeIn = visual.Compositor.CreateExpressionAnimation(
            //    "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 0 : ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 1");
            //fadeIn.SetReferenceParameter("scrollViewer", properties);
            //fadeIn.SetReferenceParameter("properties", ProfileHeader.Properties);

            visual.StartAnimation("Translation.Y", translation);
            media.Clip.StartAnimation("TopInset", clip);

            //border.StartAnimation("Opacity", fadeOut);

            menu.Opacity = 0;
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (MediaFrame.Content is INavigablePage tabPage)
            {
                tabPage.OnBackRequested(args);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (PowerSavingPolicy.AreSmoothTransitionsEnabled && SettingsService.Current.Diagnostics.ConnectedAnimationsDebug)
            {
                ProfileHeader.AnimateEntrance();
            }

            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.PageType, tab.Parameter, new SuppressNavigationTransitionInfo());
            }

            InitializeScrolling();

            if (ViewModel.IsSavedMessages)
            {
                ProfileHeader.Margin = new Thickness(0, 0, 0, -8);
                ShowHideSubtitle(true);
            }

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);

            var visual4 = ElementComposition.GetElementVisual(BackButton);
            visual4.CenterPoint = new Vector3(24, 16, 0);

            if (ProfileHeader.Visibility == Visibility.Visible && !ViewModel.IsSavedMessages)
            {
                var expOut2 = "clamp(1 - ((-(scrollViewer.Translation.Y + 148) / 32) * 0.2), 0.8, 1)";
                var slideOut2 = properties.Compositor.CreateExpressionAnimation($"vector3({expOut2}, {expOut2}, 1)");
                slideOut2.SetReferenceParameter("scrollViewer", properties);

                var slideOut3 = properties.Compositor.CreateExpressionAnimation("-clamp(((-(scrollViewer.Translation.Y + 148) / 32) * 16), 0, 16)");
                slideOut3.SetReferenceParameter("scrollViewer", properties);

                visual4.StartAnimation("Scale", slideOut2);
                visual4.StartAnimation("Translation.Y", slideOut3);
            }
            else
            {
                visual4.Scale = new Vector3(0.8f);
                visual4.Properties.InsertVector3("Translation", new Vector3(0, -16, 0));
            }

            ProfileHeader.InitializeScrolling(properties);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.Delegate = null;

            if (_notifyCollectionChanged != null)
            {
                _notifyCollectionChanged.CollectionChanged -= OnCollectionChanged;
                _notifyCollectionChanged = null;
            }

            if (MediaFrame.Content is ProfileSavedMessagesTabPage savedMessagesPage)
            {
                var args = new NavigatingEventArgs
                {
                    NavigationMode = e.NavigationMode,
                    SourcePageType = ViewModel.NavigationService.CurrentPageType,
                    Parameter = ViewModel.NavigationService.CurrentPageParam,
                    Suspending = false,
                    TargetPageType = e.SourcePageType,
                    TargetPageParameter = e.Parameter
                };

                savedMessagesPage.ViewModel.NavigatingFrom(args);
                savedMessagesPage.ViewModel.NavigatedFrom(null, false);
                savedMessagesPage.Deactivate(true);
            }
            else if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (PowerSavingPolicy.AreSmoothTransitionsEnabled && SettingsService.Current.Diagnostics.ConnectedAnimationsDebug && e.SourcePageType == typeof(ChatPage) && ViewModel.NavigationService.TryGetChatFromParameter(e.Parameter, out ChatMessageTopic nextTopic))
            {
                if (nextTopic.MessageTopic.IsDirectMessagesChat(ViewModel.ChatId))
                {
                    ProfileHeader.PrepareExit();
                }
                else if (ViewModel.ChatId == nextTopic.ChatId && ViewModel.ChatId != ViewModel.ClientService.Options.MyId && ViewModel.Topic.AreTheSame(nextTopic.MessageTopic))
                {
                    ProfileHeader.PrepareExit();
                }
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SharedCount") && ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.PageType, null, new SuppressNavigationTransitionInfo());
            }
        }

        #region Date visibility

        private void ShowHideDateHeader(bool show, bool animate)
        {
            if (_dateHeaderCollapsed != show)
            {
                return;
            }

            _dateHeaderCollapsed = !show;
            DateHeader.Visibility = show || animate ? Visibility.Visible : Visibility.Collapsed;

            _dateHeaderPanel ??= ElementComposition.GetElementVisual(DateHeader);

            if (!animate)
            {
                _dateHeaderPanel.Opacity = show ? 1 : 0;
                return;
            }

            var batch = _dateHeaderPanel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                DateHeader.Visibility = _dateHeaderCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var opacity = _dateHeaderPanel.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            _dateHeaderPanel.StartAnimation("Opacity", opacity);

            batch.End();
        }

        #endregion

        private void UpdateBackButton()
        {
            BackButton.RequestedTheme = ScrollingHost.VerticalOffset >= ProfileHeader.OccludedHeight
                ? ElementTheme.Default
                : ProfileHeader.HeaderTheme;
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            ProfileHeader.UpdateChat(chat);

            UpdateBackButton();
        }

        public void UpdateChatTitle(Chat chat)
        {
            ProfileHeader.UpdateChatTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            ProfileHeader.UpdateChatPhoto(chat);
        }

        public void UpdateChatLastMessage(Chat chat)
        {
            ProfileHeader.UpdateChatLastMessage(chat);
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            ProfileHeader.UpdateChatEmojiStatus(chat);
        }

        public void UpdateChatAccentColors(Chat chat)
        {
            ProfileHeader.UpdateChatAccentColors(chat);

            UpdateBackButton();
        }

        public void UpdateChatGifts(Chat chat)
        {
            ProfileHeader.UpdateChatGifts(chat);

            // TODO: this should be optimized, not the best approach at all
            var item = ViewModel?.Items.FirstOrDefault(x => x.PageType == typeof(ProfileGiftsTabPage));
            if (item != null)
            {
                var container = Navigation.ContainerFromItem(item) as SelectorItem;

                var grid = container?.Content as Grid;
                if (grid == null)
                {
                    return;
                }

                UpdateChatGifts(chat, grid);

                container.Content = grid;
                container.ContentTemplate = null;
            }
        }

        private void UpdateChatGifts(Chat chat, Grid grid)
        {
            if (grid.Children.Count == 4)
            {
                return;
            }

            for (int i = 0; i < Math.Min(3, ViewModel.GiftsTab.Items.Count); i++)
            {
                var gift = ViewModel.GiftsTab.Items[i] as ReceivedGift;
                var animated = new AnimatedImage
                {
                    Source = DelayedFileSource.FromSticker(ViewModel.ClientService, gift.GetSticker()),
                    Width = 20,
                    Height = 20,
                    FrameSize = new Windows.Foundation.Size(20, 20),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    IsViewportAware = true,
                    LoopCount = 3,
                    Margin = new Thickness(4, 0, 0, 0),
                };

                Grid.SetColumn(animated, grid.Children.Count);
                grid.Children.Add(animated);
            }
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            ProfileHeader.UpdateChatActiveStories(chat);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            ProfileHeader.UpdateChatNotificationSettings(chat);
        }

        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            ProfileHeader.UpdateUserFullInfo(chat, user, fullInfo, secret, accessToken);
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            ProfileHeader.UpdateUserStatus(chat, user);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            ProfileHeader.UpdateSecretChat(chat, secretChat);
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateBasicGroupFullInfo(chat, group, fullInfo);

            if (fullInfo != null)
            {
                ViewModel.Members = new SortedObservableCollection<ChatMember>(fullInfo.Members, new ChatMemberComparer(ViewModel.ClientService, true));
            }
        }



        public void UpdateSupergroup(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateSupergroupFullInfo(chat, group, fullInfo);

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }
        }

        #endregion

        private long _itemsSourceToken;
        private long _selectionModeToken;

        private bool _backgroundUpdated;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (_notifyCollectionChanged != null)
            {
                _notifyCollectionChanged.CollectionChanged -= OnCollectionChanged;
                _notifyCollectionChanged = null;
            }

            if (MediaFrame.Content is ProfileSavedMessagesTabPage savedMessagesPage)
            {
                var args = new NavigatingEventArgs
                {
                    NavigationMode = e.NavigationMode,
                    SourcePageType = ViewModel.NavigationService.CurrentPageType,
                    Parameter = ViewModel.NavigationService.CurrentPageParam,
                    Suspending = false,
                    TargetPageType = e.SourcePageType,
                    TargetPageParameter = e.Parameter
                };

                savedMessagesPage.ViewModel.NavigatingFrom(args);
                savedMessagesPage.ViewModel.NavigatedFrom(null, false);
                savedMessagesPage.Deactivate(true);
            }
            else if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is ProfileSavedMessagesTabPage savedMessagesPage)
            {
                if (!_backgroundUpdated)
                {
                    _backgroundUpdated = true;
                    BackgroundRoot.Update(ViewModel.ClientService);
                }

                BackgroundRoot.Visibility = Visibility.Visible;

                savedMessagesPage.Activate(ViewModel.NavigationService);
                savedMessagesPage.HeaderHeight = Math.Max(Header.ActualHeight, 48 + 10);
                savedMessagesPage.ViewModel.Dispatcher = ViewModel.Dispatcher;
                savedMessagesPage.ViewModel.NavigationService = ViewModel.NavigationService;
                _ = savedMessagesPage.ViewModel.NavigatedToAsync(e.Parameter, e.NavigationMode, new Telegram.Navigation.Services.NavigationState());

                if (_fromItemClick)
                {
                    _fromItemClick = false;
                    RootGrid.Unsnap();
                }
                return;
            }
            else if (_backgroundUpdated)
            {
                BackgroundRoot.Visibility = Visibility.Collapsed;
            }

            if (e.Content is not ProfileTabPage tabPage)
            {
                return;
            }

            tabPage.HeaderHeight = Math.Max(Header.ActualHeight, 48 + 10);

            if (e.Content is ProfileSavedChatsTabPage or ProfileMediaTabPage or ProfileGiftsTabPage)
            {
                Menu.Visibility = Visibility.Visible;
            }
            else
            {
                Menu.Visibility = Visibility.Collapsed;
            }

            if (e.Content is ProfileStoriesTabPage)
            {
                if (e.Parameter is ChatStoriesType type)
                {
                    tabPage.DataContext = type == ChatStoriesType.Pinned
                        ? ViewModel.PinnedStoriesTab
                        : ViewModel.ArchivedStoriesTab;
                }
                else
                {
                    tabPage.DataContext = ViewModel.PinnedStoriesTab;
                }
            }
            else if (e.Content is ProfileGiftsTabPage)
            {
                tabPage.DataContext = ViewModel.GiftsTab;
            }

            if (tabPage.ScrollingHost.ItemsSource != null)
            {
                LoadMore(tabPage.ScrollingHost);
            }

            tabPage.ScrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);

            if (e.Content is not ProfileStoriesTabPage)
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged, ref _selectionModeToken);
            }

            if (_fromItemClick)
            {
                _fromItemClick = false;
                RootGrid.Unsnap();
            }
        }

        private INotifyCollectionChanged _notifyCollectionChanged;

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            if (_notifyCollectionChanged != null)
            {
                _notifyCollectionChanged.CollectionChanged -= OnCollectionChanged;
            }

            _notifyCollectionChanged = scrollingHost.ItemsSource as INotifyCollectionChanged;

            if (_notifyCollectionChanged != null)
            {
                _notifyCollectionChanged.CollectionChanged += OnCollectionChanged;
            }

            _hasBeenScrolled = false;
            RootGrid.Unsnap();

            LoadMore(scrollingHost);
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (_hasBeenScrolled)
                {
                    ScrollToContent(true);
                }

                if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
                {
                    return;
                }

                LoadMore(scrollingHost);
            }
        }

        private void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackButton();

            MediaFrame.MinHeight = ScrollingHost.ActualHeight + e.NewSize.Height - 88;

            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.HeaderHeight = Math.Max(e.NewSize.Height, 48 + 10);
            }
        }

        private Thumb _scrollBarThumb;
        private Border _scrollBarPanningThumb;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            MediaFrame.MinHeight = Header.ActualHeight + e.NewSize.Height - 88;

            var material = ElementComposition.GetElementVisual(BackgroundRoot);
            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var width = Math.Min(24 + 1000 + 24, BackgroundRoot.ActualSize.X) - 24 - 24;
            var x = (BackgroundRoot.ActualSize.X - width) / 2;

            var elli1 = CanvasGeometry.CreateRoundedRectangle(null, x, 0, width, BackgroundRoot.ActualSize.Y, 4, 4);
            var ellipse = material.Compositor.CreatePathGeometry(new CompositionPath(elli1));
            var clip = material.Compositor.CreateGeometricClip(ellipse);

            var animation = material.Compositor.CreateExpressionAnimation("max(4, _.Translation.Y + HeaderHeight + 4)");
            animation.SetReferenceParameter("_", properties);
            animation.SetScalarParameter("HeaderHeight", Math.Max(Header.ActualSize.Y, 48 + 10));

            material.Clip = clip;
            clip.StartAnimation("Offset.Y", animation);

            var scrollBar = ScrollingHost.GetLastChild<ScrollBar>(x => x.Orientation == Orientation.Vertical);
            if (scrollBar != null && _scrollBarThumb == null)
            {
                var rootGrid = scrollBar.GetChild<Grid>();
                if (rootGrid != null)
                {
                    var visualStateGroups = VisualStateManager.GetVisualStateGroups(rootGrid);
                    if (visualStateGroups != null)
                    {
                        foreach (var group in visualStateGroups)
                        {
                            group.CurrentStateChanged += ScrollingIndicatorStates_CurrentStateChanged;
                        }
                    }
                }

                _scrollBarThumb = scrollBar.GetChild<Thumb>(x => x.Name == "VerticalThumb");
                _scrollBarPanningThumb = scrollBar.GetChild<Border>(x => x.Name == "VerticalPanningThumb");
            }

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private bool _scrollBarIndicatorVisible;
        private bool _scrollBarIndicatorEnabled;

        private void ScrollingIndicatorStates_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            switch (e.NewState.Name)
            {
                case "MouseIndicator":
                    BindToolTipToThumb(_scrollBarThumb);

                    _scrollBarIndicatorVisible = true;
                    ToolTip.Visibility = _scrollBarIndicatorEnabled && !_subtitleCollapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    break;
                case "TouchIndicator":
                    BindToolTipToThumb(_scrollBarPanningThumb);

                    _scrollBarIndicatorVisible = true;
                    ToolTip.Visibility = _scrollBarIndicatorEnabled && !_subtitleCollapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    break;
                case "NoIndicator":
                    _scrollBarIndicatorVisible = false;
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;

                // This part is needed to fix the visual state (that is still broken all across the app)
                case "Collapsed":
                    if (e.OldState.Name == "Expanded")
                    {
                        VisualStateManager.GoToState(ScrollingHost, "NoIndicator", true);
                    }
                    break;
            }
        }

        private void BindToolTipToThumb(UIElement element)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(ToolTip, true);

            var target = ElementComposition.GetElementVisual(element);

            if (element is not Thumb)
            {
                element = VisualTreeHelper.GetParent(element) as UIElement;
            }

            if (element is not null)
            {
                var visual = ElementComposition.GetElementVisual(ToolTip);
                var thumb = ElementComposition.GetElementVisual(element);

                var animation = visual.Compositor.CreateExpressionAnimation("thumb.Offset.Y + (target.Size.Y - this.Target.Size.Y) / 2");
                animation.SetReferenceParameter("thumb", thumb);
                animation.SetReferenceParameter("target", target);

                visual.StartAnimation("Translation.Y", animation);
            }
        }

        private bool _initialViewChanging = true;
        private bool _initialDirectManipulation;
        private double _initialVerticalOffset;
        private double _initialFinalOffset;
        private double _initialPreviousOffset;

        private bool _hasBeenScrolled;

        private void OnDirectManipulationStarted(object sender, object e)
        {
            _initialDirectManipulation = true;
            _hasBeenScrolled = true;
        }

        private void OnDirectManipulationCompleted(object sender, object e)
        {
            _initialDirectManipulation = false;
            _hasBeenScrolled = false;
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            _hasBeenScrolled = true;
        }

        private void OnViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (ViewModel.IsSavedMessages || !_hasBeenScrolled)
            {
                return;
            }

            if (e.IsInertial && e.NextView.VerticalOffset.AlmostEquals(e.FinalView.VerticalOffset, 1e-02))
            {
                _initialViewChanging = true;
                return;
            }

            if (_initialViewChanging)
            {
                _initialVerticalOffset = ScrollingHost.VerticalOffset;
                _initialFinalOffset = e.FinalView.VerticalOffset;
                Logger.Info("Initial: " + _initialVerticalOffset);
            }
            else if (_initialFinalOffset == e.FinalView.VerticalOffset && e.IsInertial)
            {
                return;
            }

            _initialViewChanging = false;
            _initialFinalOffset = e.FinalView.VerticalOffset;

            // Direction changed
            if (_initialDirectManipulation && !e.IsInertial && (_initialVerticalOffset < _initialFinalOffset) != (_initialPreviousOffset < e.NextView.VerticalOffset))
            {
                _initialVerticalOffset = _initialPreviousOffset;
                _initialFinalOffset = e.FinalView.VerticalOffset;
                Logger.Info("Direction changed");
            }

            _initialPreviousOffset = e.NextView.VerticalOffset;

            if (e.NextView.VerticalOffset <= ProfileHeader.HeaderHeight - 48)
            {
                var diff = e.NextView.VerticalOffset - (ProfileHeader.HeaderHeight - 48);
                var diff2 = e.NextView.VerticalOffset - e.FinalView.VerticalOffset;

                PanelScrollingDirection direction;
                if (_initialDirectManipulation)
                {
                    direction = _initialVerticalOffset > e.FinalView.VerticalOffset /*&& diff2.AlmostEqualsToZero()*/
                            ? PanelScrollingDirection.Backward
                            : _initialVerticalOffset < e.FinalView.VerticalOffset || Math.Abs(diff) > 24
                            ? PanelScrollingDirection.Forward
                            : PanelScrollingDirection.Backward;
                }
                else
                {
                    direction = e.NextView.VerticalOffset < e.FinalView.VerticalOffset || Math.Abs(diff) > 24
                        ? PanelScrollingDirection.Forward
                        : PanelScrollingDirection.Backward;
                }

                var contentOffset = Math.Max(ProfileHeader.ActualSize.Y - 24, 48 + 10);
                var snap = ProfileHeader.ActualSize.Y > ProfileHeader.HeaderHeight
                    ? _initialVerticalOffset >= contentOffset
                    ? contentOffset
                    : ProfileHeader.HeaderHeight - 48
                    : contentOffset;

                if (RootGrid.Snap(direction == PanelScrollingDirection.Forward ? snap : 0, !_initialDirectManipulation))
                {
                    if (direction == PanelScrollingDirection.Forward)
                    {
                        Logger.Info("Snap header");
                    }
                    else
                    {
                        Logger.Info("Unsnap header");
                    }
                }
            }
            else
            {
                var diff = e.FinalView.VerticalOffset - (ProfileHeader.ActualSize.Y - 48);
                var threshold = _initialDirectManipulation ? 24 : 32;

                if (RootGrid.Snap(diff >= -1 && diff <= threshold ? Math.Max(ProfileHeader.ActualSize.Y - 24, 48 + 10) : -1, false))
                {
                    if (diff >= 0 && diff <= threshold)
                    {
                        Logger.Info("Snap content");
                    }
                    else
                    {
                        Logger.Info("Unsnap content");
                    }
                }
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ScrollingHost.VerticalOffset == 0 && !e.IsIntermediate && !_hasBeenScrolled)
            {
                ScrollToContent(true);
                return;
            }

            if (!e.IsIntermediate && RootGrid.Unsnap())
            {
                Logger.Info("Unsnap");
            }

            UpdateBackButton();

            if (ProfileHeader.Visibility == Visibility.Visible && !ViewModel.IsSavedMessages)
            {
                ProfileHeader.ViewChanged(ScrollingHost, (float)ScrollingHost.VerticalOffset);
                ShowHideSubtitle(ScrollingHost.VerticalOffset >= ProfileHeader.ActualHeight - 48);
            }

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                _scrollBarIndicatorEnabled = false;
                ToolTip.Visibility = Visibility.Collapsed;
                return;
            }

            LoadMore(scrollingHost);

            var index = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.FirstVisibleIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.FirstVisibleIndex,
                _ => -1
            };

            if (scrollingHost.ItemsSource is MediaDataSource dataSource)
            {
                int date = -1;
                if (index >= 0 && scrollingHost.Items[index] is MessageWithOwner message)
                {
                    date = message.Date;
                }
                else
                {
                    var diff = ScrollingHost.VerticalOffset - (ProfileHeader.ActualSize.Y - 48);

                    var offset = diff / ScrollingHost.ScrollableHeight;
                    if (offset >= 0)
                    {
                        var position = dataSource.GetByOffset(offset);
                        if (position != null)
                        {
                            date = position.Date;
                        }
                    }
                }

                if (date >= 0 && dataSource.HasPositions)
                {
                    ToolTipContent.Text = Formatter.Date(date, Strings.formatterMonthYear);

                    _scrollBarIndicatorEnabled = true;
                    ToolTip.Visibility = _scrollBarIndicatorVisible && !_subtitleCollapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else
                {
                    _scrollBarIndicatorEnabled = false;
                    ToolTip.Visibility = Visibility.Collapsed;
                }

                _dateHeaderTimer.Stop();
                ShowHideDateHeader(false, false);
            }
            else if (index >= 0 && index < scrollingHost.Items.Count)
            {
                _scrollBarIndicatorEnabled = false;
                ToolTip.Visibility = Visibility.Collapsed;

                var container = scrollingHost.Items[index];
                if (container is MessageWithOwner message)
                {
                    DateHeaderLabel.Text = Formatter.Date(message.Date, Strings.formatterMonthYear);
                }
                else if (container is StoryViewModel story)
                {
                    DateHeaderLabel.Text = Formatter.Date(story.Date, Strings.formatterMonthYear);
                }
                else
                {
                    return;
                }

                _dateHeaderTimer.Stop();
                _dateHeaderTimer.Start();
                ShowHideDateHeader(ScrollingHost.VerticalOffset > ProfileHeader.ActualHeight, true);
            }
        }

        private bool _subtitleCollapsed = true;

        private void ShowHideSubtitle(bool show)
        {
            if (_subtitleCollapsed != show)
            {
                return;
            }

            _subtitleCollapsed = !show;

            var cardBackground = ElementComposition.GetElementVisual(CardBackground);
            var menu = ElementComposition.GetElementVisual(MenuRoot);

            var opacityOut = cardBackground.Compositor.CreateScalarKeyFrameAnimation();
            opacityOut.InsertKeyFrame(0, show ? 1 : 0);
            opacityOut.InsertKeyFrame(1, show ? 0 : 1);

            var opacityIn = cardBackground.Compositor.CreateScalarKeyFrameAnimation();
            opacityIn.InsertKeyFrame(0, show ? 0 : 1);
            opacityIn.InsertKeyFrame(1, show ? 1 : 0);

            cardBackground.StartAnimation("Opacity", opacityOut);
            menu.StartAnimation("Opacity", opacityIn);

            ScrollingHost.SetVerticalPadding(show ? 88 : 0, 0);

            ToolTip.Visibility = _scrollBarIndicatorEnabled && _scrollBarIndicatorVisible && !_subtitleCollapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private bool _loadingMore;

        private async void LoadMore(ListViewBase scrollingHost)
        {
            if (_loadingMore)
            {
                return;
            }

            _loadingMore = true;

            uint loadedMore = 0;
            int lastCacheIndex = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.LastCacheIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.LastCacheIndex,
                _ => -1
            };

            var needsMore = lastCacheIndex == scrollingHost.Items.Count - 1;
            needsMore |= scrollingHost.ActualHeight < ScrollingHost.ActualHeight;

            if (needsMore && scrollingHost.ItemsSource is ISupportIncrementalLoading supportIncrementalLoading && supportIncrementalLoading.HasMoreItems)
            {
                var result = await supportIncrementalLoading.LoadMoreItemsAsync(50);
                loadedMore = result.Count;
            }

            _loadingMore = false;

            if (loadedMore > 0)
            {
                LoadMore(scrollingHost);
            }
        }

        private bool _fromItemClick;

        private void Navigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (Navigation.SelectedItem == e.ClickedItem)
            {
                _fromItemClick = false;
                ScrollToContent(false);
            }
            else
            {
                _fromItemClick = true;
            }
        }

        private void ScrollToContent(bool disableAnimation)
        {
            _hasBeenScrolled = false;
            RootGrid.Unsnap();

            Logger.Info(disableAnimation + ", " + ScrollingHost.ScrollableHeight);
            ScrollingHost.TryChangeView(null, ViewModel.IsSavedMessages ? 0 : Math.Round(ProfileHeader.ActualSize.Y - 24), null, disableAnimation);
        }

        private int _prevSelectedIndex = -1;

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is ProfileTabItem page && (page.Parameter != null || page.PageType != MediaFrame.Content?.GetType()))
            {
                Logger.Info(page.PageType);

                NavigationTransitionInfo transition = _prevSelectedIndex == -1
                    ? new SuppressNavigationTransitionInfo()
                    : new SlideNavigationTransitionInfo
                    {
                        Effect = _prevSelectedIndex < Navigation.SelectedIndex
                            ? SlideNavigationTransitionEffect.FromRight
                            : SlideNavigationTransitionEffect.FromLeft
                    };

                _hasBeenScrolled = false;
                RootGrid.Unsnap();

                _prevSelectedIndex = Navigation.SelectedIndex;
                MediaFrame.Navigate(page.PageType, page.Parameter, transition);
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (MediaFrame.Content is ProfileSavedChatsTabPage)
            {
                flyout.CreateFlyoutItem(ViewModel.SendMessage, Strings.SavedViewAsMessages, Icons.ChatEmpty);
            }
            else if (MediaFrame.Content is ProfileMediaTabPage)
            {
                var zoomIn = new MenuFlyoutItem
                {
                    Text = Strings.MediaZoomIn,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.ZoomIn)
                };

                var zoomOut = new MenuFlyoutItem
                {
                    Text = Strings.MediaZoomOut,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.ZoomOut)
                };

                var calendar = new MenuFlyoutItem
                {
                    Text = Strings.Calendar,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Calendar)
                };

                var photos = new MenuFlyoutItem
                {
                    Text = Strings.MediaShowPhotos,
                    Icon = ViewModel.Media.Filter is SearchMessagesFilterPhoto or SearchMessagesFilterPhotoAndVideo ? MenuFlyoutHelper.CreateIcon(Icons.Checkmark) : null
                };

                var videos = new MenuFlyoutItem
                {
                    Text = Strings.MediaShowVideos,
                    Icon = ViewModel.Media.Filter is SearchMessagesFilterVideo or SearchMessagesFilterPhotoAndVideo ? MenuFlyoutHelper.CreateIcon(Icons.Checkmark) : null
                };

                zoomIn.Click += MediaZoomIn_Click;
                zoomOut.Click += MediaZoomOut_Click;
                calendar.Click += MediaCalendar_Click;

                photos.Click += MediaShowPhotos_Click;
                videos.Click += MediaShowVideos_Click;

                //if (SettingsService.Current.Diagnostics.SparseMessagesDebug)
                //{
                //    flyout.Items.Add(zoomIn);
                //    flyout.Items.Add(zoomOut);
                //}

                if (ViewModel.Media.UseDataSource && ViewModel.Media.DataSource.HasPositions)
                {
                    flyout.Items.Add(calendar);
                    flyout.CreateFlyoutSeparator();
                }

                flyout.Items.Add(photos);
                flyout.Items.Add(videos);
            }
            else if (MediaFrame.Content is ProfileGiftsTabPage)
            {
                var sort = new MenuFlyoutItem
                {
                    Text = ViewModel.GiftsTab.SortByPrice
                        ? Strings.Gift2FilterSortByValue
                        : Strings.Gift2FilterSortByDate,
                    Icon = MenuFlyoutHelper.CreateIcon(ViewModel.GiftsTab.SortByPrice ? Icons.DollarArrowUp : Icons.CalendarArrowUp)
                };

                var unlimited = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterUnlimited,
                    Icon = ViewModel.GiftsTab.ExcludeUnlimited ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var limited = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterLimited,
                    Icon = ViewModel.GiftsTab.ExcludeNonUpgradable ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var upgradable = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterUpgradable,
                    Icon = ViewModel.GiftsTab.ExcludeUpgradable ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var unique = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterUnique,
                    Icon = ViewModel.GiftsTab.ExcludeUpgraded ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                void UpdateFilters(Action action)
                {
                    _hasBeenScrolled = false;
                    RootGrid.Unsnap();

                    action();
                }

                sort.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.SortByPrice = !ViewModel.GiftsTab.SortByPrice);
                unlimited.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeUnlimited = !ViewModel.GiftsTab.ExcludeUnlimited);
                limited.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeNonUpgradable = !ViewModel.GiftsTab.ExcludeNonUpgradable);
                upgradable.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeUpgradable = !ViewModel.GiftsTab.ExcludeUpgradable);
                unique.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeUpgraded = !ViewModel.GiftsTab.ExcludeUpgraded);

                flyout.Items.Add(sort);
                flyout.CreateFlyoutSeparator();
                flyout.Items.Add(unlimited);
                flyout.Items.Add(limited);
                flyout.Items.Add(upgradable);
                flyout.Items.Add(unique);

                if (ViewModel.GiftsTab.IsOwned)
                {
                    var displayed = new MenuFlyoutItem
                    {
                        Text = Strings.Gift2FilterDisplayed,
                        Icon = ViewModel.GiftsTab.ExcludeSaved ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                    };

                    var hidden = new MenuFlyoutItem
                    {
                        Text = Strings.Gift2FilterHidden,
                        Icon = ViewModel.GiftsTab.ExcludeUnsaved ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                    };

                    displayed.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeSaved = !ViewModel.GiftsTab.ExcludeSaved);
                    hidden.Click += (s, args) => UpdateFilters(() => ViewModel.GiftsTab.ExcludeUnsaved = !ViewModel.GiftsTab.ExcludeUnsaved);

                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(displayed);
                    flyout.Items.Add(hidden);
                }
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MediaZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media)
            {
                media.Zoom(-1);
            }
        }

        private void MediaZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media)
            {
                media.Zoom(1);
            }
        }

        private async void MediaCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (MediaFrame.Content is ProfileMediaTabPage media && ViewModel.Media.UseDataSource)
            {
                var popup = new CalendarPopup(ViewModel.ClientService, ViewModel.Chat.Id, ViewModel.Topic);
                popup.MaxDate = DateTimeOffset.Now.Date;

                var confirm = await ViewModel.ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary && popup.SelectedDates.Count > 0)
                {
                    var first = popup.SelectedDates.FirstOrDefault();
                    var offset = first.Date.ToTimestamp();

                    var closest = ViewModel.Media.DataSource.GetByDate(offset);
                    var panel = media.ScrollingHost.ItemsPanelRoot as ItemsWrapGrid;

                    int x = closest.Position % panel.MaximumRowsOrColumns;
                    int y = closest.Position / panel.MaximumRowsOrColumns;

                    _hasBeenScrolled = false;
                    RootGrid.Unsnap();

                    ScrollingHost.TryChangeView(null, (ViewModel.IsSavedMessages ? ProfileHeader.ActualHeight - 48 + 24 : 0) + (y * panel.ItemHeight), null, false);
                }
            }
        }

        private void MediaShowPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Media.Filter is SearchMessagesFilterPhotoAndVideo)
            {
                ViewModel.Media.Filter = new SearchMessagesFilterVideo();
            }
            else if (ViewModel.Media.Filter is SearchMessagesFilterVideo)
            {
                ViewModel.Media.Filter = new SearchMessagesFilterPhotoAndVideo();
            }
        }

        private void MediaShowVideos_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Media.Filter is SearchMessagesFilterPhotoAndVideo)
            {
                ViewModel.Media.Filter = new SearchMessagesFilterPhoto();
            }
            else if (ViewModel.Media.Filter is SearchMessagesFilterPhoto)
            {
                ViewModel.Media.Filter = new SearchMessagesFilterPhotoAndVideo();
            }
        }

        #region Selection

        private string ConvertSelection(int count)
        {
            return Locale.Declension(Strings.R.messages, count);
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is ListViewBase selector)
            {
                ShowHideManagePanel(selector.SelectionMode == ListViewSelectionMode.Multiple);
            }
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ManagePanel.Visibility = _manageCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();
        }

        #endregion

        private void Navigation_PrepareContainerForItem(SelectorItem sender, object args)
        {
            if (args is ProfileTabItem item && item.PageType == typeof(ProfileGiftsTabPage))
            {
                var textBlock = new TextBlock
                {
                    Text = item.Text,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.ColumnDefinitions.Add(1, GridUnitType.Auto);
                grid.Children.Add(textBlock);

                UpdateChatGifts(ViewModel.Chat, grid);

                sender.Content = grid;
                sender.ContentTemplate = null;
            }
        }

        private void Navigation_ItemContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var tabItem = Navigation.ItemFromContainer(sender) as ProfileTabItem;
            if (tabItem.CanSetAsMain && (ViewModel.MyProfile || (ViewModel.Chat.Type is ChatTypeSupergroup { IsChannel: true } && ViewModel.Chat.CanChangeInfo(ViewModel.ClientService))))
            {
                var flyout = new MenuFlyout();

                // TODO: icon is missing
                flyout.CreateFlyoutItem(ViewModel.SetMainTab, tabItem.Type, Strings.ProfileTabSetAsMain);
                flyout.ShowAt(sender, args);
            }
        }
    }

    public partial class ProfileSnapGrid : Grid, IScrollSnapPointsInfo
    {
        public IReadOnlyList<float> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment alignment)
        {
            if (_headerHeight == -1)
            {
                return new float[0];
            }

            if (_snapToTop)
            {
                return new float[]
                {
                    0,
                    _headerHeight
                };
            }

            return new float[]
            {
                _headerHeight
            };
        }

        public float GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment alignment, out float offset)
        {
            offset = 0;
            return 0;
        }

        private float _headerHeight = -1;
        private bool _snapToTop = false;

        public float HeaderHeight => _headerHeight;

        public bool Unsnap()
        {
            return Snap(-1, false);
        }

        public bool Snap(float headerHeight, bool snapToTop)
        {
            if (_headerHeight == headerHeight && _snapToTop == snapToTop)
            {
                return false;
            }

            _headerHeight = headerHeight;
            _snapToTop = snapToTop;
            VerticalSnapPointsChanged?.Invoke(this, null);

            return true;
        }

        public bool AreHorizontalSnapPointsRegular => false;

        public bool AreVerticalSnapPointsRegular => false;

        public event EventHandler<object> HorizontalSnapPointsChanged;
        public event EventHandler<object> VerticalSnapPointsChanged;
    }
}
