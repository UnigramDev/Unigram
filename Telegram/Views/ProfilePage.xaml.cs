//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.ComponentModel;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Stories;
using Telegram.Views.Chats;
using Telegram.Views.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class ProfilePage : HostedPage, IProfileDelegate, INavigablePage
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        private CompositionPropertySet _properties;

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

            InitializeScrolling();
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
                    ScrollingHost.Loaded -= handler;
                    ScrollingHost.ChangeView(null, listViewPosition.ScrollPosition, null, true);
                }

                ScrollingHost.Loaded += handler;
            }
        }

        private void InitializeScrolling()
        {
            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var visual = ElementComposition.GetElementVisual(HeaderPanel);
            var border = ElementComposition.GetElementVisual(CardBackground);
            var clipper = ElementComposition.GetElementVisual(ClipperBackground);

            ElementCompositionPreview.SetIsTranslationEnabled(HeaderPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(BackButton, true);

            _properties = visual.Compositor.CreatePropertySet();
            _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y + 16);

            var translation = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -properties.ActualHeight ? 0 : -scrollViewer.Translation.Y - properties.ActualHeight : -scrollViewer.Translation.Y");
            translation.SetReferenceParameter("scrollViewer", properties);
            translation.SetReferenceParameter("properties", _properties);

            var fadeOut = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 1 : 1 - ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 0");
            fadeOut.SetReferenceParameter("scrollViewer", properties);
            fadeOut.SetReferenceParameter("properties", _properties);

            var fadeIn = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 0 : ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 1");
            fadeIn.SetReferenceParameter("scrollViewer", properties);
            fadeIn.SetReferenceParameter("properties", _properties);

            visual.StartAnimation("Translation.Y", translation);

            border.StartAnimation("Opacity", fadeOut);
            clipper.StartAnimation("Opacity", fadeIn);
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.OnBackRequested(args);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, tab.Parameter, new SuppressNavigationTransitionInfo());
            }

            var visual4 = ElementComposition.GetElementVisual(BackButton);
            visual4.CenterPoint = new Vector3(24, 16, 0);

            if (ProfileHeader.Visibility == Visibility.Visible)
            {
                var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);

                var expOut2 = "clamp(1 - ((-(scrollViewer.Translation.Y + 164) / 32) * 0.2), 0.8, 1)";
                var slideOut2 = properties.Compositor.CreateExpressionAnimation($"vector3({expOut2}, {expOut2}, 1)");
                slideOut2.SetReferenceParameter("scrollViewer", properties);

                var expOut3y = "-clamp(((-(scrollViewer.Translation.Y + 164) / 32) * 16), 0, 16)";
                var expOut3x = "-clamp(((-(scrollViewer.Translation.Y + properties.ActualHeight - 32) / 32) * 16), 0, 16)";
                var slideOut3 = properties.Compositor.CreateExpressionAnimation($"vector3({expOut3x}, {expOut3y}, 0)");
                slideOut3.SetReferenceParameter("scrollViewer", properties);
                slideOut3.SetReferenceParameter("properties", _properties);

                visual4.StartAnimation("Scale", slideOut2);
                visual4.StartAnimation("Translation", slideOut3);

                ProfileHeader.InitializeScrolling(properties);
            }
            else
            {
                visual4.Scale = new Vector3(0.8f);
                visual4.Properties.InsertVector3("Translation", new Vector3(-12, -16, 0));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SharedCount") && ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, null, new SuppressNavigationTransitionInfo());
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

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            ProfileHeader.UpdateChat(chat);

            Menu.Visibility = chat.Id == ViewModel.ClientService.Options.MyId && ViewModel.SavedMessagesTopic == null && !ViewModel.MyProfile
                ? Visibility.Visible
                : Visibility.Collapsed;

            BackButton.RequestedTheme = ScrollingHost.VerticalOffset < ProfileHeader.ActualHeight - 16 || !ProfileHeader.IsLoaded
                ? ProfileHeader.HeaderTheme
                : ElementTheme.Default;
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

            BackButton.RequestedTheme = ScrollingHost.VerticalOffset < ProfileHeader.ActualHeight - 16
                ? ProfileHeader.HeaderTheme
                : ElementTheme.Default;
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            ProfileHeader.UpdateChatActiveStories(chat);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            ProfileHeader.UpdateChatNotificationSettings(chat);
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            ProfileHeader.UpdateUser(chat, user, secret);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
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



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            ProfileHeader.UpdateBasicGroup(chat, group);
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateBasicGroupFullInfo(chat, group, fullInfo);

            ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ClientService, true), fullInfo.Members);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            ProfileHeader.UpdateSupergroup(chat, group);

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ProfileHeader.UpdateSupergroupFullInfo(chat, group, fullInfo);
        }

        #endregion

        private long _itemsSourceToken;
        private long _selectionModeToken;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is not ProfileTabPage tabPage)
            {
                return;
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

            if (tabPage.ScrollingHost.ItemsSource != null)
            {
                LoadMore(tabPage.ScrollingHost);
            }
            else
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);
            }

            if (e.Content is not ProfileStoriesTabPage)
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged, ref _selectionModeToken);
            }
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y + 16);
            ViewModel.HeaderHeight = Math.Max(e.NewSize.Height, 48 + 10);
            MediaFrame.MinHeight = ScrollingHost.ActualHeight + e.NewSize.Height - 48;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            MediaFrame.MinHeight = Header.ActualHeight + e.NewSize.Height - 48;

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            BackButton.RequestedTheme = ScrollingHost.VerticalOffset < ProfileHeader.ActualHeight - 16
                ? ProfileHeader.HeaderTheme
                : ElementTheme.Default;

            if (ProfileHeader.Visibility == Visibility.Visible)
            {
                ProfileHeader.ViewChanged(ScrollingHost.VerticalOffset);
            }

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);

            var index = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.FirstVisibleIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.FirstVisibleIndex,
                _ => -1
            };

            if (index < 0 || index >= scrollingHost.Items.Count)
            {
                return;
            }

            var container = scrollingHost.Items[index];
            if (container is MessageWithOwner message)
            {
                DateHeaderLabel.Text = Formatter.MonthGrouping(message.Date);
            }
            else if (container is StoryViewModel story)
            {
                DateHeaderLabel.Text = Formatter.MonthGrouping(story.Date);
            }
            else
            {
                return;
            }

            _dateHeaderTimer.Stop();
            _dateHeaderTimer.Start();
            ShowHideDateHeader(ScrollingHost.VerticalOffset > ProfileHeader.ActualHeight, true);
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

        private int _prevSelectedIndex = -1;

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is ProfileTabItem page && (page.Parameter != null || page.Type != MediaFrame.Content?.GetType()))
            {
                NavigationTransitionInfo transition = _prevSelectedIndex == -1
                    ? new SuppressNavigationTransitionInfo()
                    : new SlideNavigationTransitionInfo
                    {
                        Effect = _prevSelectedIndex < Navigation.SelectedIndex
                            ? SlideNavigationTransitionEffect.FromRight
                            : SlideNavigationTransitionEffect.FromLeft
                    };

                _prevSelectedIndex = Navigation.SelectedIndex;
                MediaFrame.Navigate(page.Type, page.Parameter, transition);
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

            var user = chat.Type is ChatTypePrivate or ChatTypeSecret ? ViewModel.ClientService.GetUser(chat) : null;
            if (user != null && user.Id == ViewModel.ClientService.Options.MyId)
            {
                flyout.CreateFlyoutItem(ViewModel.SendMessage, Strings.SavedViewAsMessages, Icons.ChatEmpty);
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
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
    }
}
