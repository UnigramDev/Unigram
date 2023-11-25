//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Views;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Stories;
using Telegram.Views.BasicGroups;
using Telegram.Views.Channels;
using Telegram.Views.Chats;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Users;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Point = Windows.Foundation.Point;

namespace Telegram.Views
{
    public sealed partial class MainPage : Page, IRootContentPage, INavigatingPage, IChatListDelegate
    {
        private MainViewModel _viewModel;
        public MainViewModel ViewModel => _viewModel ??= DataContext as MainViewModel;

        public RootPage Root { get; set; }

        private readonly IClientService _clientService;

        private readonly AnimatedListHandler _handler;

        private readonly DispatcherTimer _memoryUsageTimer;
        private double _memoryUsage;

        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MainViewModel>();

            _clientService = ViewModel.ClientService;

            _handler = new AnimatedListHandler(ChatsList, AnimatedListType.Other);

            ViewModel.Chats.Delegate = this;
            ViewModel.PlaybackService.SourceChanged += OnPlaybackSourceChanged;

            InitializeTitleBar();
            InitializeSearch();
            InitializeLock();

            DropShadowEx.Attach(UpdateShadow);

            ChatsList.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            //var show = !((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats;
            //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            ArchivedChatsPanel.Visibility = ViewModel.Settings.Settings.HideArchivedChats
                ? Visibility.Collapsed
                : Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);

            if (SettingsService.Current.Diagnostics.ShowMemoryUsage)
            {
                _memoryUsageTimer = new DispatcherTimer();
                _memoryUsageTimer.Interval = TimeSpan.FromSeconds(1);
                _memoryUsageTimer.Tick += MemoryUsageTimer_Tick;
                _memoryUsageTimer.Start();

                MemoryUsageTimer_Tick(null, null);
            }
        }

        private void MemoryUsageTimer_Tick(object sender, object e)
        {
            var memoryUsage = Math.Round(Windows.System.MemoryManager.AppMemoryUsage / 1024.0 / 1024.0);
            if (memoryUsage != _memoryUsage)
            {
                _memoryUsage = memoryUsage;
                MemoryLabel.Text = $"- {memoryUsage:F0} MB";
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Initialize();
            Window.Current.SetTitleBar(TitleBarHandle);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.SetTitleBar(null);
        }

        public void Dispose()
        {
            try
            {
                Bindings.StopTracking();
                DataContext = null;

                var viewModel = _viewModel;
                if (viewModel != null)
                {
                    viewModel.PlaybackService.SourceChanged -= OnPlaybackSourceChanged;

                    viewModel.Settings.Delegate = null;
                    viewModel.Chats.Delegate = null;

                    viewModel.Aggregator.Unsubscribe(this);
                    viewModel.Dispose();
                }

                MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated -= OnNavigated;
                MasterDetail.Dispose();
                SettingsView?.Dispose();

                _viewModel = null;

                if (_memoryUsageTimer != null)
                {
                    _memoryUsageTimer.Tick -= MemoryUsageTimer_Tick;
                    _memoryUsageTimer.Stop();
                }
            }
            catch { }
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;
            sender.IsVisibleChanged += OnLayoutMetricsChanged;
            sender.LayoutMetricsChanged += OnLayoutMetricsChanged;

            OnLayoutMetricsChanged(sender, null);
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            try
            {
                TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 0), GridUnitType.Pixel);
                TitleBarrr.ColumnDefinitions[4].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 0), GridUnitType.Pixel);

                Grid.SetColumn(TitleBarLogo, sender.SystemOverlayLeftInset > 0 ? 3 : 1);
                StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;

                Photo.HorizontalAlignment = sender.SystemOverlayLeftInset > 0
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
            }
            catch
            {
                // Most likely InvalidComObjectException
            }
        }

        private void InitializeLock()
        {
            Lock.Visibility = ViewModel.Passcode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Handle

        public void UpdateChatLastMessage(Chat chat)
        {
            Handle(chat, (chatView, chat) =>
            {
                chatView.UpdateChatReadInbox(chat);
                chatView.UpdateChatLastMessage(chat);
            });

            if (chat.LastMessage != null
                && chat.Id == _viewModel.Topics.Chat?.Id
                && _viewModel.Topics.Items.TryGetValue(chat.LastMessage.MessageThreadId, out ForumTopic topic))
            {
                topic.LastMessage = chat.LastMessage;

                this.BeginOnUIThread(() =>
                {
                    var container = TopicList.ContainerFromItem(topic) as ListViewItem;
                    var topicView = container?.ContentTemplateRoot as ForumTopicCell;

                    topicView?.UpdateForumTopicLastMessage(topic);
                });
            }
        }

        public void Handle(UpdateChatActiveStories update)
        {
            if (update.ActiveStories.List is StoryListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive()));
            }
            else
            {
                Handle(update.ActiveStories.ChatId, (chatView, chat) => chatView.UpdateChatActiveStories(update.ActiveStories));
            }
        }

        public void Handle(UpdateFileDownloads update)
        {
            this.BeginOnUIThread(() => Downloads.UpdateFileDownloads(update));
        }

        public void Handle(UpdateChatPosition update)
        {
            if (update.Position.List is ChatListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive()));
            }
        }

        public void Handle(UpdateChatIsMarkedAsUnread update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
        }

        public void Handle(UpdateChatReadInbox update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
        }

        public void Handle(UpdateChatReadOutbox update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadOutbox(chat));
        }

        public void Handle(UpdateChatUnreadMentionCount update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateChatUnreadReactionCount update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateChatTitle update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatTitle(chat));
        }

        public void Handle(UpdateChatPhoto update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatPhoto(chat));
        }

        public void Handle(UpdateChatVideoChat update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatVideoChat(chat));
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == _clientService.Options.MyId)
            {
                this.BeginOnUIThread(() => UpdateUser(update.User));
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (update.UserId != _clientService.Options.MyId && update.UserId != 777000 && _clientService.TryGetChatFromUser(update.UserId, out long chatId))
            {
                Handle(chatId, (chatView, chat) => chatView.UpdateUserStatus(chat, update.Status));
            }
        }

        public void Handle(UpdateChatAction update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatActions(chat, ViewModel.ClientService.GetChatActions(chat.Id)));

            if (update.ChatId == _viewModel.Topics.Chat?.Id
                && update.MessageThreadId != 0
                && _viewModel.Topics.Items.TryGetValue(update.ChatId, out ForumTopic topic))
            {
                this.BeginOnUIThread(() =>
                {
                    var container = TopicList.ContainerFromItem(topic) as ListViewItem;
                    var topicView = container?.ContentTemplateRoot as ForumTopicCell;

                    topicView?.UpdateForumTopicActions(topic, ViewModel.ClientService.GetChatActions(update.ChatId, update.MessageThreadId));
                });
            }
        }

        public void Handle(UpdateMessageMentionRead update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateMessageUnreadReactions update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateMessageContent update)
        {
            Handle(update.ChatId, update.MessageId, chat => chat.LastMessage.Content = update.NewContent, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
        }

        public async void Handle(UpdateSecretChat update)
        {
            var response = await _clientService.SendAsync(new CreateSecretChat(update.SecretChat.Id));
            if (response is Chat result)
            {
                Handle(result.Id, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
            }
        }

        public void Handle(UpdateChatNotificationSettings update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateNotificationSettings(chat));
        }

        public void Handle(UpdateUnreadChatCount update)
        {
            if (update.ChatList is ChatListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ClientService, update.ChatList));
            }
        }

        public void Handle(UpdateForumTopicInfo update)
        {
            if (_viewModel.Topics.Chat?.Id == update.ChatId
                && _viewModel.Topics.Items.TryGetValue(update.Info.MessageThreadId, out ForumTopic topic))
            {
                topic.Info = update.Info;

                this.BeginOnUIThread(() =>
                {
                    var container = TopicList.ContainerFromItem(topic) as ListViewItem;
                    var topicView = container?.ContentTemplateRoot as ForumTopicCell;

                    topicView?.UpdateForumTopicInfo(topic);
                });
            }
        }

        private void Handle(long chatId, long messageId, Action<Chat> update, Action<ChatCell, Chat> action)
        {
            var chat = _clientService.GetChat(chatId);
            if (chat.LastMessage == null || chat.LastMessage.Id != messageId)
            {
                return;
            }

            update(chat);

            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetCell(chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(long chatId, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetChatAndCell(chatId, out Chat chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(Chat chat, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetCell(chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        public void Handle(UpdateChatFolders update)
        {
            this.BeginOnUIThread(() =>
            {
                ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && update.ChatFolders.Count > 0);
                ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && update.ChatFolders.Count > 0);
                ShowHideArchive(ViewModel.SelectedFolder?.ChatList is ChatListMain or null);

                UpdatePaneToggleButtonVisibility();
            });
        }

        public void Handle(UpdatePasscodeLock update)
        {
            this.BeginOnUIThread(() =>
            {
                Lock.Visibility = update.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public void Handle(UpdateConfetti update)
        {
            this.BeginOnUIThread(() =>
            {
                FindName(nameof(Confetti));
                Confetti.Start();
            });
        }

        public void Handle(UpdateUnconfirmedSession update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.Session == null)
                {
                    UnloadObject(UnconfirmedCard);
                }
                else
                {
                    FindName(nameof(UnconfirmedCard));
                    UnconfirmedCard.Update(update.Session);
                }
            });
        }

        public void Handle(UpdateConnectionState update)
        {
            this.BeginOnUIThread(() =>
            {
                SetProxyVisibility(_clientService.Options.ExpectBlocking, _clientService.Options.EnabledProxyId, update.State);

                switch (update.State)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                        ShowState(Strings.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting connecting:
                        ShowState(Strings.Connecting);
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        ShowState(Strings.ConnectingToProxy);
                        break;
                    case ConnectionStateUpdating updating:
                        ShowState(Strings.Updating);
                        break;
                    case ConnectionStateReady ready:
                        HideState();
                        return;
                }
            });
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name.Equals("expect_blocking") || update.Name.Equals("enabled_proxy_id"))
            {
                this.BeginOnUIThread(() => SetProxyVisibility(_clientService.Options.ExpectBlocking, _clientService.Options.EnabledProxyId, _clientService.ConnectionState));
            }
        }

        private void SetProxyVisibility(bool expectBlocking, long proxyId, ConnectionState connectionState)
        {
            if (expectBlocking || proxyId != 0)
            {
                Proxy.Visibility = Visibility.Visible;
            }
            else
            {
                switch (connectionState)
                {
                    case ConnectionStateWaitingForNetwork:
                    case ConnectionStateConnecting:
                    case ConnectionStateConnectingToProxy:
                        Proxy.Visibility = Visibility.Visible;
                        break;
                    default:
                        Proxy.Visibility = Visibility.Collapsed;
                        break;
                }
            }

            Proxy.Glyph = connectionState is ConnectionStateReady && proxyId != 0 ? Icons.ShieldCheckmark : Icons.ShieldError;
        }

        private void ShowState(string text)
        {
            State.IsIndeterminate = true;
            StateLabel.Text = text;

            var peer = FrameworkElementAutomationPeer.FromElement(TitleText);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);

            try
            {
                ApplicationView.GetForCurrentView().Title = text;
            }
            catch { }
        }

        private void HideState()
        {
            State.IsIndeterminate = false;
#if DEBUG && !MOCKUP
            StateLabel.Text = Strings.AppName;
#else
            StateLabel.Text = "Unigram";
#endif

            try
            {
                ApplicationView.GetForCurrentView().Title = string.Empty;
            }
            catch { }
        }

        public void Handle(UpdateCallDialog update)
        {
            void UpdatePlaybackHidden(bool hidden)
            {
                if (Playback != null)
                {
                    Playback.IsHidden = hidden;
                }
            }

            this.BeginOnUIThread(() =>
            {
                if (update.Call != null && update.Call.IsValidState())
                {
                    UpdatePlaybackHidden(true);
                    FindName(nameof(CallBanner));
                }
                else if (update.GroupCall != null && (update.GroupCall.IsJoined || update.GroupCall.NeedRejoin))
                {
                    UpdatePlaybackHidden(true);
                    FindName(nameof(GroupCallBanner));

                    GroupCallBanner.Update(ViewModel.VoipGroupService);
                }
                else
                {
                    UpdatePlaybackHidden(false);

                    if (CallBanner != null)
                    {
                        UnloadObject(CallBanner);
                    }

                    if (GroupCallBanner != null)
                    {
                        GroupCallBanner.Update(null);
                        UnloadObject(GroupCallBanner);
                    }
                }
            });
        }

        public void Handle(UpdateChatFoldersLayout update)
        {
            this.BeginOnUIThread(() =>
            {
                ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0);
                ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0);
            });
        }

        #endregion

        public void ShowChatsUndo(IList<Chat> chats, UndoType type, Action<IList<Chat>> undo, Action<IList<Chat>> action = null)
        {
            Undo.Show(chats, type, undo, action);
        }

        private bool _tabsTopCollapsed = true;
        private bool _tabsLeftCollapsed = true;

        private void ShowHideTopTabs(bool show)
        {
            if (_tabsTopCollapsed != show)
            {
                return;
            }

            _tabsTopCollapsed = !show;
            FindName(nameof(ChatTabs));

            void ShowHideTopTabsCompleted()
            {
                DialogsPanel.Margin = new Thickness();
                ChatTabs.Visibility = _tabsTopCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideTopTabsCompleted();
                return;
            }

            ChatTabs.Visibility = Visibility.Visible;
            DialogsPanel.Margin = new Thickness(0, 0, 0, -40);

            var visual = ElementCompositionPreview.GetElementVisual(DialogsPanel);
            var header = ElementCompositionPreview.GetElementVisual(ChatTabsView);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ShowHideTopTabsCompleted();
            };

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -32, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            //offset.Duration = Constants.FastAnimation;

            var opacity1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration /= 2;

            var opacity2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration /= 2;

            header.StartAnimation("Offset", offset);
            visual.StartAnimation("Offset", offset);

            batch.End();
        }

        private void ShowHideLeftTabs(bool show)
        {
            if (_tabsLeftCollapsed != show)
            {
                return;
            }

            _tabsLeftCollapsed = !show;
            FindName(nameof(ChatTabsLeft));

            Root?.SetSidebarEnabled(show);

            Stories.TabsLeftCollapsed = !show;

            TitleBarrr.Margin = new Thickness(show ? 14 : 48, 0, 0, 0);

            Photo.Width = show ? 72 : 48;
            Photo.Visibility = show || MasterDetail.MasterVisibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (ChatTabsLeft == null)
            {
                FindName(nameof(ChatTabsLeft));
            }

            void ShowHideLeftTabsCompleted()
            {
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
                ChatTabsLeft.Visibility = _tabsLeftCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideLeftTabsCompleted();
                return;
            }

            ChatTabsLeft.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -40);

            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var header = ElementCompositionPreview.GetElementVisual(ChatTabsView);

            parent.Clip = null;

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ShowHideLeftTabsCompleted();
            };

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            //offset.Duration = Constants.FastAnimation;

            var opacity1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration /= 2;

            var opacity2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration /= 2;

            header.StartAnimation("Offset", offset);
            visual.StartAnimation("Offset", offset);

            batch.End();
        }

        public void OnBackRequesting(HandledEventArgs args)
        {
            if (Root?.IsPaneOpen is true)
            {
                Root.IsPaneOpen = false;
                args.Handled = true;
            }
            else if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
                args.Handled = true;
            }
            else if (!_topicListCollapsed)
            {
                ShowHideTopicList(false);
                UpdateListViewsSelectedItem(0);
                args.Handled = true;
            }
            else if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                Manage_Click(null, null);
                args.Handled = true;
            }
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            OnBackRequesting(args);

            if (args.Handled)
            {
                return;
            }

            if (rpMasterTitlebar.SelectedIndex > 0)
            {
                SetPivotIndex(0);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
                args.Handled = true;
            }
            else
            {
                var scrollViewer = ChatsList.GetScrollViewer();
                if (scrollViewer != null && scrollViewer.VerticalOffset > 50)
                {
                    scrollViewer.ChangeView(null, 0, null);
                    args.Handled = true;
                }
                else if (ViewModel.Chats.Items.ChatList is ChatListArchive
                    || ViewModel.Folders.Count > 0 && !ViewModel.Chats.Items.ChatList.ListEquals(ViewModel.Folders[0].ChatList))
                {
                    UpdateFolder(ViewModel.Folders.Count > 0 ? ViewModel.Folders[0] : ChatFolderViewModel.Main);
                    args.Handled = true;
                }
            }
        }

        private void UpdateUser(User user)
        {
            TitleBarLogo.IsEnabled = _clientService.IsPremium;

            if (user.EmojiStatus != null)
            {
                LogoBasic.Visibility = Visibility.Collapsed;
                LogoPremium.Visibility = Visibility.Collapsed;

                FindName(nameof(LogoEmoji));
                LogoEmoji.Source = new CustomEmojiFileSource(_clientService, user.EmojiStatus.CustomEmojiId);
            }
            else
            {
                LogoBasic.Visibility = _clientService.IsPremium ? Visibility.Collapsed : Visibility.Visible;
                LogoPremium.Visibility = _clientService.IsPremium ? Visibility.Visible : Visibility.Collapsed;

                UnloadObject(LogoEmoji);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                UpdateUser(user);
            }

            Subscribe();
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;

            OnStateChanged(null, null);

            ShowHideBanner(ViewModel.PlaybackService.CurrentItem != null);

            var update = new UpdateConnectionState(ViewModel.ClientService.ConnectionState);
            if (update.State != null)
            {
                Handle(update);
                ViewModel.Aggregator.Publish(update);
            }

            Handle(new UpdateUnconfirmedSession(ViewModel.ClientService.UnconfirmedSession));

            if (_unloaded)
            {
                _unloaded = false;
                ChatsList.ItemsSource = null;

                Bindings.StopTracking();
                Bindings.Update();
            }

            if (SettingsService.Current.Diagnostics.IsLastErrorDiskFull)
            {
                SettingsService.Current.Diagnostics.IsLastErrorDiskFull = false;

                // TODO: Missing translation
                var confirm = await ViewModel.ShowPopupAsync("Unigram has previously failed to launch because the device storage was full.\r\n\r\nMake sure there's enough storage space available and press **OK** to continue.", "Disk storage is full", Strings.OK, Strings.StorageUsage);
                if (confirm == ContentDialogResult.Secondary)
                {
                    MasterDetail.NavigationService.Navigate(typeof(SettingsStoragePage));
                }
            }
        }

        private void Subscribe()
        {
            ViewModel.Aggregator.Subscribe<UpdateFileDownloads>(this, Handle)
                .Subscribe<UpdateChatActiveStories>(Handle)
                .Subscribe<UpdateChatPosition>(Handle)
                .Subscribe<UpdateChatIsMarkedAsUnread>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateChatReadOutbox>(Handle)
                .Subscribe<UpdateChatUnreadMentionCount>(Handle)
                .Subscribe<UpdateChatUnreadReactionCount>(Handle)
                .Subscribe<UpdateChatTitle>(Handle)
                .Subscribe<UpdateChatPhoto>(Handle)
                .Subscribe<UpdateChatVideoChat>(Handle)
                .Subscribe<UpdateUserStatus>(Handle)
                .Subscribe<UpdateUser>(Handle)
                .Subscribe<UpdateChatAction>(Handle)
                .Subscribe<UpdateMessageMentionRead>(Handle)
                .Subscribe<UpdateMessageUnreadReactions>(Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateForumTopicInfo>(Handle)
                //.Subscribe<UpdateMessageContent>(Handle)
                .Subscribe<UpdateSecretChat>(Handle)
                .Subscribe<UpdateChatFolders>(Handle)
                .Subscribe<UpdateChatNotificationSettings>(Handle)
                .Subscribe<UpdatePasscodeLock>(Handle)
                .Subscribe<UpdateUnconfirmedSession>(Handle)
                .Subscribe<UpdateConnectionState>(Handle)
                .Subscribe<UpdateOption>(Handle)
                .Subscribe<UpdateCallDialog>(Handle)
                .Subscribe<UpdateChatFoldersLayout>(Handle)
                .Subscribe<UpdateConfetti>(Handle);
        }

        private void OnPlaybackSourceChanged(IPlaybackService sender, object e)
        {
            this.BeginOnUIThread(() => ShowHideBanner(sender.CurrentItem != null));
        }

        private bool _bannerCollapsed;

        private async void ShowHideBanner(bool show)
        {
            if (show && Playback == null)
            {
                FindName(nameof(Playback));
                Playback.Update(ViewModel.ClientService, ViewModel.PlaybackService, ViewModel.NavigationService);
            }

            return;

            if ((show && Playback.Visibility == Visibility.Visible) || (!show && (Playback.Visibility == Visibility.Collapsed || _bannerCollapsed)))
            {
                return;
            }

            if (show)
            {
                _bannerCollapsed = false;
            }
            else
            {
                _bannerCollapsed = true;
            }

            Playback.Visibility = Visibility.Visible;
            await Playback.UpdateLayoutAsync();

            var detail = ElementCompositionPreview.GetElementVisual(MasterDetail.NavigationService.Frame);
            var playback = ElementCompositionPreview.GetElementVisual(Playback);

            var batch = detail.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                detail.Offset = new Vector3();
                MasterDetail.NavigationService.Frame.Margin = new Thickness();

                if (show)
                {
                    _bannerCollapsed = false;
                }
                else
                {
                    Playback.Visibility = Visibility.Collapsed;
                }
            };

            var y = Playback.ActualSize.Y;

            MasterDetail.NavigationService.Frame.Margin = new Thickness(0, 0, 0, -y);

            float y0, y1;

            if (show)
            {
                y0 = -y;
                y1 = 0;
            }
            else
            {
                y0 = 0;
                y1 = -y;
            }

            var offset0 = detail.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, y0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, y1, 0));
            detail.StartAnimation("Offset", offset0);
            playback.StartAnimation("Offset", offset0);

            batch.End();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;

            var titleBar = CoreApplication.GetCurrentView().TitleBar;
            titleBar.IsVisibleChanged -= OnLayoutMetricsChanged;
            titleBar.LayoutMetricsChanged -= OnLayoutMetricsChanged;

            Bindings.StopTracking();

            _unloaded = true;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (MasterDetail.NavigationService?.Frame.Content is not BlankPage)
            {
                return;
            }

            var character = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0 || char.IsControl(character[0]) || char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                Search_Click(null, null);
                SearchField.Focus(FocusState.Keyboard);
                SearchField.Text = character;
                SearchField.SelectionStart = character.Length;

                args.Handled = true;
            }
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            var invoked = ViewModel?.ShortcutService.Process(args);
            if (invoked == null)
            {
                return;
            }

            foreach (var command in invoked.Commands)
            {
                if (SettingsService.Current.Diagnostics.ShowMemoryUsage && command == ShortcutCommand.Quit)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    return;
                }

                ProcessChatCommands(command, args);
                ProcessFolderCommands(command, args);
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, InputKeyDownEventArgs args)
        {
            if (command is ShortcutCommand.SetStatus)
            {
                Status_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.Search)
            {
                if (MasterDetail.NavigationService.Frame.Content is ISearchablePage child)
                {
                    child.Search();
                }
                else
                {
                    SearchField.Focus(FocusState.Keyboard);
                    Search_Click(null, null);
                }

                args.Handled = true;
            }
            else if (command is ShortcutCommand.SearchChats)
            {
                SearchField.Focus(FocusState.Keyboard);
                Search_Click(null, null);

                args.Handled = true;
            }
            else if (command is ShortcutCommand.Quit)
            {
                await SystemTray.ExitAsync();
                await BootStrapper.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Close)
            {
                await SystemTray.CloseRequestedAsync();
                await WindowContext.Current.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Lock)
            {
                Lock_Click(null, null);
            }
            else if (command is ShortcutCommand.Downloads)
            {
                Downloads_Click(null, null);
            }
        }

        private void ProcessFolderCommands(ShortcutCommand command, InputKeyDownEventArgs args)
        {
            var folders = ViewModel.Folders;
            if (folders.Empty())
            {
                return;
            }

            if (command == ShortcutCommand.FolderPrevious)
            {
                args.Handled = true;
                ScrollFolder(-1, true);
            }
            else if (command == ShortcutCommand.FolderNext)
            {
                args.Handled = false;
                ScrollFolder(+1, true);
            }
            else if (command == ShortcutCommand.ShowAllChats)
            {
                args.Handled = true;
                ScrollFolder(int.MinValue, true);
            }
            else if (command == ShortcutCommand.ShowFolderLast)
            {
                args.Handled = true;
                ScrollFolder(int.MaxValue, true);
            }
            else if (command == ShortcutCommand.ShowArchive)
            {
                args.Handled = true;
                ArchivedChats_Click(null, null);
            }
            else if (command is >= ShortcutCommand.ShowFolder1 and <= ShortcutCommand.ShowFolder6)
            {
                var index = command - ShortcutCommand.ShowAllChats;
                if (folders.Count > index)
                {
                    UpdateFolder(folders[index], false);
                }
            }
        }

        private async void ProcessChatCommands(ShortcutCommand command, InputKeyDownEventArgs args)
        {
            if (command == ShortcutCommand.ChatPrevious)
            {
                args.Handled = true;
                Scroll(-1, true);
            }
            else if (command == ShortcutCommand.ChatNext)
            {
                args.Handled = true;
                Scroll(+1, true);
            }
            else if (command == ShortcutCommand.ChatFirst)
            {
                args.Handled = true;
                Scroll(int.MinValue, true);
            }
            else if (command == ShortcutCommand.ChatLast)
            {
                args.Handled = true;
                Scroll(int.MaxValue, true);
            }
            else if (command == ShortcutCommand.ChatSelf)
            {
                args.Handled = true;

                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(ViewModel.ClientService.Options.MyId, false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    MasterDetail.NavigationService.GoBackAt(0, false);
                }
            }
            else if (command is >= ShortcutCommand.ChatPinned1 and <= ShortcutCommand.ChatPinned5)
            {
                var folders = ViewModel.Folders;
                if (folders.Count > 0)
                {
                    return;
                }

                var index = command - ShortcutCommand.ChatPinned1;

                var response = await ViewModel.ClientService.GetChatListAsync(new ChatListMain(), 0, (int)ViewModel.ClientService.Options.PinnedChatCountMax * 2 + 1);
                if (response is Telegram.Td.Api.Chats chats && index >= 0 && index < chats.ChatIds.Count)
                {
                    for (int i = 0; i < chats.ChatIds.Count; i++)
                    {
                        var chat = ViewModel.ClientService.GetChat(chats.ChatIds[i]);
                        if (chat == null)
                        {
                            return;
                        }

                        //if (chat.Source != null)
                        //{
                        //    index++;
                        //}
                        //else if (i == index)
                        //{
                        //    if (chat.IsPinned)
                        //    {
                        //        MasterDetail.NavigationService.NavigateToChat(chats.ChatIds[index]);
                        //        MasterDetail.NavigationService.GoBackAt(0, false);
                        //    }

                        //    return;
                        //}
                    }
                }
            }
        }

        public void Scroll(int offset, bool navigate)
        {
            int index;
            if (offset == int.MaxValue)
            {
                index = ViewModel.Chats.Items.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index = ChatsList.SelectedIndex + offset;
            }

            if (index >= 0 && index < ViewModel.Chats.Items.Count)
            {
                if (navigate)
                {
                    Navigate(ViewModel.Chats.Items[index]);
                }
            }
            else if (index < 0 && offset == -1 && !navigate)
            {
                Search_Click(null, null);
            }
        }

        public void ScrollFolder(int offset, bool navigate)
        {
            var already = ViewModel.SelectedFolder;
            if (already == null)
            {
                return;
            }

            var index = ViewModel.Folders.IndexOf(already);
            if (offset == int.MaxValue)
            {
                index = ViewModel.Folders.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index += offset;
            }

            if (index >= 0 && index < ViewModel.Folders.Count)
            {
                UpdateFolder(ViewModel.Folders[index], false);
            }
        }

        public void Initialize()
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main", Frame, ViewModel);
                MasterDetail.NavigationService.FrameFacade.Navigating += OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
            ViewModel.Chats.NavigationService = MasterDetail.NavigationService;
            ViewModel.Contacts.NavigationService = MasterDetail.NavigationService;
            ViewModel.Calls.NavigationService = MasterDetail.NavigationService;
            ViewModel.Settings.NavigationService = MasterDetail.NavigationService;

            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        public async void Activate(string parameter)
        {
            Initialize();

            if (parameter == null)
            {
                return;
            }

            if (parameter.StartsWith("tg:toast"))
            {
                parameter = parameter.Substring("tg:toast?".Length);
            }
            else if (parameter.StartsWith("tg://toast"))
            {
                parameter = parameter.Substring("tg://toast?".Length);
            }

            if (Uri.TryCreate(parameter, UriKind.Absolute, out Uri scheme))
            {
                Activate(scheme);
            }
            else
            {
                var data = Toast.SplitArguments(parameter);
                if (data.ContainsKey("from_id") && int.TryParse(data["from_id"], out int from_id))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
                else if (data.ContainsKey("chat_id") && int.TryParse(data["chat_id"], out int chat_id))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreateBasicGroupChat(chat_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
                else if (data.ContainsKey("channel_id") && int.TryParse(data["channel_id"], out int channel_id))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreateSupergroupChat(channel_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
            }

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    if (popup.Child is GalleryWindow gallery)
                    {
                        gallery.OnBackRequested(new BackRequestedRoutedEventArgs());
                        break;
                    }
                }
            }
        }

        public async void Activate(Uri scheme)
        {
            if (App.DataPackages.TryRemove(0, out DataPackageView package))
            {
                await MasterDetail.NavigationService.ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationDataPackage(package));
            }

            if (MessageHelper.IsTelegramUrl(scheme))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, MasterDetail.NavigationService, scheme);
            }
            else if (scheme.Scheme.Equals("ms-contact-profile") || scheme.Scheme.Equals("ms-ipmessaging"))
            {
                var query = scheme.Query.ParseQueryString();
                if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
            }
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            var allowed = e.SourcePageType == typeof(ChatPage) ||
                e.SourcePageType == typeof(ChatPinnedPage) ||
                e.SourcePageType == typeof(ChatThreadPage) ||
                e.SourcePageType == typeof(ChatScheduledPage) ||
                e.SourcePageType == typeof(ChatEventLogPage) ||
                e.SourcePageType == typeof(BlankPage); //||
                                                       //frame.CurrentSourcePageType == typeof(ChatPage) ||
                                                       //frame.CurrentSourcePageType == typeof(ChatPinnedPage) ||
                                                       //frame.CurrentSourcePageType == typeof(ChatThreadPage) ||
                                                       //frame.CurrentSourcePageType == typeof(ChatScheduledPage) ||
                                                       //frame.CurrentSourcePageType == typeof(ChatEventLogPage) ||
                                                       //frame.CurrentSourcePageType == typeof(BlankPage);

            var type = allowed ? BackgroundKind.Background : BackgroundKind.Material;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal && e.SourcePageType == typeof(BlankPage))
            {
                type = BackgroundKind.None;
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(type, true);
            }
        }

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                MasterDetail.AllowCompact = true;
            }
            else
            {
                MasterDetail.AllowCompact = e.SourcePageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == 0;
            }

            _shouldGoBackWithDetail = true;

            UpdatePaneToggleButtonVisibility();
            UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetChatFromBackStack());
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ChatsList.SelectedItem = null;
                    ChatsList.SelectionMode = ListViewSelectionMode.None;
                }

                Header.Visibility = Visibility.Visible;
                TitleText.Visibility = Visibility.Visible;
            }
            else
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ChatsList.SelectionMode = ListViewSelectionMode.Single;
                    ChatsList.SelectedItem = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == ViewModel.Chats.SelectedItem);
                }

                Header.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
                TitleText.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdatePaneToggleButtonVisibility();

            ChatsList.UpdateViewState(MasterDetail.CurrentState);

            var frame = MasterDetail.NavigationService.Frame;
            var allowed = frame.CurrentSourcePageType == typeof(ChatPage) ||
                frame.CurrentSourcePageType == typeof(ChatPinnedPage) ||
                frame.CurrentSourcePageType == typeof(ChatThreadPage) ||
                frame.CurrentSourcePageType == typeof(ChatScheduledPage) ||
                frame.CurrentSourcePageType == typeof(ChatEventLogPage) ||
                frame.CurrentSourcePageType == typeof(BlankPage);

            var type = allowed ? BackgroundKind.Background : BackgroundKind.Material;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal && frame.CurrentSourcePageType == typeof(BlankPage))
            {
                type = BackgroundKind.None;
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(type, false);
            }
        }

        private void OnMasterVisibilityChanged(object sender, EventArgs e)
        {
            Photo.Visibility = MasterDetail.MasterVisibility == Visibility.Visible || !_tabsLeftCollapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdatePaneToggleButtonVisibility()
        {
            var visible = ViewModel.Chats.Items.ChatList is ChatListArchive
                || !_searchCollapsed
                || !_topicListCollapsed
                || rpMasterTitlebar.SelectedIndex != 0;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                visible &= MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage);
            }

            Photo.IsChecked = visible;
            //Photo.Glyph = visible
            //    ? Photo.HorizontalAlignment == HorizontalAlignment.Right
            //    ? Icons.ArrowRight
            //    : Icons.ArrowLeft
            //    : Icons.Hamburger;
        }

        private void UpdateListViewsSelectedItem(long chatId)
        {
            ViewModel.Chats.SelectedItem = chatId;

            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                if (ViewModel.ClientService.TryGetChat(chatId, out Chat chat) && ViewModel.Chats.Items.Contains(chat))
                {
                    if (ChatsList.SelectedItem != chat)
                    {
                        ChatsList.SelectedItem = chat;
                    }
                }
                else
                {
                    ChatsList.SelectedItem = null;
                }
            }
        }

        public bool EvaluatePaneToggleButtonVisibility()
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                return MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage);
            }

            return true;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple && e.ClickedItem is Chat chat)
            {
                if (ViewModel.Chats.SelectedItems.Contains(chat))
                {
                    ViewModel.Chats.SelectedItems.Remove(chat);
                }
                else
                {
                    ViewModel.Chats.SelectedItems.Add(chat);
                }

                if (ViewModel.Chats.SelectedItems.Empty())
                {
                    ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                        ? ListViewSelectionMode.None
                        : ListViewSelectionMode.Single;
                }
            }
            else
            {
                Navigate(e.ClickedItem);
            }
        }

        public async void Navigate(object item)
        {
#if MOCKUP
            if (item is Chat cat)
            {
                if (cat.Id == 0)
                {
                    MasterDetail.NavigationService.Navigate(typeof(ChatPage), 9L);
                }
                else if (cat.Id == 1)
                {
                    MasterDetail.NavigationService.Navigate(typeof(ChatPage), 10L);
                }
            }

            ChatsList.SelectedItem = null;

            return;
#endif

            if (item is TLCallGroup callGroup)
            {
                item = callGroup.Message;
            }

            if (item is Message message)
            {
                ViewModel.Chats.SelectedItem = message.ChatId;

                MasterDetail.NavigationService.NavigateToChat(
                    message.ChatId,
                    message: message.Id,
                    thread: message.IsTopicMessage ? message.MessageThreadId : null,
                    force: false);
            }
            else
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
            }

            if (item is TLCallGroup group)
            {
                item = group.Message;
            }
            else if (item is SearchResult result)
            {
                if (result.Chat != null)
                {
                    item = result.Chat;
                    ViewModel.ClientService.Send(new AddRecentlyFoundChat(result.Chat.Id));
                }
                else
                {
                    item = result.User;
                }
            }

            //if (item is TLMessageCommonBase message)
            //{
            //    if (message.Parent != null)
            //    {
            //        MasterDetail.NavigationService.NavigateToDialog(message.Parent, message.Id);
            //    }
            //}
            //else
            //{
            //    SearchField.Text = string.Empty;
            //}

            if (item is User user)
            {
                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                if (response is Chat)
                {
                    item = response as Chat;
                }
            }

            if (item is Chat chat)
            {
                ViewModel.Chats.SelectedItem = chat.Id;

                if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup) && supergroup.IsForum)
                {
                    if (ViewModel.Chats.SelectedItem != ViewModel.Topics.Chat?.Id)
                    {
                        ViewModel.Topics.SetFilter(chat);
                        ShowHideTopicList(true);
                    }
                    else
                    {
                        ViewModel.Topics.SetFilter(null);
                        ShowHideTopicList(false);
                        UpdateListViewsSelectedItem(0);
                    }
                }
                else
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    MasterDetail.NavigationService.GoBackAt(0, false);

                    ViewModel.Topics.SetFilter(null);
                    ShowHideTopicList(false);
                }
            }
            else if (item is ForumTopic topic)
            {
                ViewModel.Chats.SelectedItem = ViewModel.Topics.Chat?.Id;
                MasterDetail.NavigationService.NavigateToThread(ViewModel.Topics.Chat, topic.Info.MessageThreadId);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateManage();
        }

        private void InitializeSearch()
        {
            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                if (rpMasterTitlebar.SelectedIndex == 0)
                {
                    //var items = ViewModel.Chats.Search;
                    //if (items != null && string.Equals(SearchField.Text, items.Query))
                    //{
                    //    await items.LoadMoreItemsAsync(2);
                    //    await items.LoadMoreItemsAsync(3);
                    //    await items.LoadMoreItemsAsync(4);
                    //}
                }
                else if (rpMasterTitlebar.SelectedIndex == 1)
                {
                    var items = ViewModel.Contacts.Search;
                    if (items != null && string.Equals(SearchField.Text, items.Query))
                    {
                        await items.LoadMoreItemsAsync(1);
                        await items.LoadMoreItemsAsync(2);
                    }
                }
                else if (rpMasterTitlebar.SelectedIndex == 3)
                {
                    var items = ViewModel.Contacts.Search;
                    if (items != null && string.Equals(SearchField.Text, items.Query))
                    {
                        await items.LoadMoreItemsAsync(1);
                        await items.LoadMoreItemsAsync(2);
                    }
                }
            };
        }

        #region Context menu

        private void Call_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var call = element.Tag as TLCallGroup;

            flyout.CreateFlyoutItem(ViewModel.Calls.DeleteCall, call, Strings.Delete, Icons.Delete, destructive: true);

            args.ShowAt(flyout, element);
        }

        #endregion

        private void NewContact_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(UserCreatePage));
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == 0;

            switch (rpMasterTitlebar.SelectedIndex)
            {
                case 0:
                    Root?.SetSelectedIndex(RootDestination.Chats);
                    break;
                case 1:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Contacts);
                    SearchField.ControlledList = null;

                    FindName(nameof(ContactsRoot));
                    break;
                case 2:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Calls);
                    SearchField.ControlledList = null;

                    FindName(nameof(CallsRoot));
                    break;
                case 3:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Settings);
                    SearchField.ControlledList = null;

                    if (SettingsView == null)
                    {
                        FindName(nameof(SettingsRoot));
                        SettingsView.DataContext = ViewModel.Settings;
                        ViewModel.Settings.Delegate = SettingsView;
                    }
                    break;
            }

            SearchField.Text = string.Empty;

            UpdateHeader();
            UpdatePaneToggleButtonVisibility();

            SearchReset();

            if (rpMasterTitlebar.SelectedIndex != 0)
            {
                Stories.Collapse();
            }

            _shouldGoBackWithDetail = false;

            for (int i = 0; i < ViewModel.Children.Count; i++)
            {
                if (ViewModel.Children[i] is IChildViewModel child)
                {
                    if (i == rpMasterTitlebar.SelectedIndex)
                    {
                        child.Activate();
                    }
                    else
                    {
                        child.Deactivate();
                    }
                }
            }
        }

        private void UpdateHeader()
        {
            ChatsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            SearchField.PlaceholderText = rpMasterTitlebar.SelectedIndex == 3 ? Strings.SearchInSettings : Strings.Search;
        }

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;

            FindName(nameof(DialogsSearchPanel));
            DialogsPanel.Visibility = Visibility.Visible;
            DialogsSearchPanel.Visibility = Visibility.Visible;

            if (show)
            {
                SearchField.ControlledList = DialogsSearchPanel.Root;
                Stories.Collapse();
            }

            var chats = ElementCompositionPreview.GetElementVisual(DialogsPanel);
            var panel = ElementCompositionPreview.GetElementVisual(DialogsSearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3(DialogsPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                DialogsPanel.Visibility = _searchCollapsed ? Visibility.Visible : Visibility.Collapsed;
                DialogsSearchPanel.Visibility = _searchCollapsed ? Visibility.Collapsed : Visibility.Visible;
            };

            var scale1 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(1.05f, 1.05f, 1));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
            scale1.Duration = TimeSpan.FromMilliseconds(200);

            var scale2 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(0.95f, 0.95f, 1));
            scale2.Duration = TimeSpan.FromMilliseconds(200);

            var opacity1 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = TimeSpan.FromMilliseconds(200);

            var opacity2 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = TimeSpan.FromMilliseconds(200);

            panel.StartAnimation("Scale", scale1);
            panel.StartAnimation("Opacity", opacity1);

            chats.StartAnimation("Scale", scale2);
            chats.StartAnimation("Opacity", opacity2);

            batch.End();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Keyboard && sender == SearchField)
            {
                return;
            }

            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == 0;

            SearchReset();

            UpdatePaneToggleButtonVisibility();
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrWhiteSpace(SearchField.Text))
            {
                return;
            }

            _shouldGoBackWithDetail = false;

            MasterDetail.AllowCompact = false;

            if (rpMasterTitlebar.SelectedIndex == 0)
            {
                ShowHideSearch(true);
                ViewModel.SearchChats.Query = SearchField.Text;
            }
            else if (rpMasterTitlebar.SelectedIndex == 1)
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    SearchReset();
                }
                else
                {
                    ContactsPanel.Visibility = Visibility.Collapsed;

                    var items = ViewModel.Contacts.Search = new SearchUsersCollection(ViewModel.ClientService, SearchField.Text);
                    await items.LoadMoreItemsAsync(0);
                }
            }
            else if (rpMasterTitlebar.SelectedIndex == 3)
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    SearchReset();
                }
                else
                {
                    if (SettingsView != null)
                    {
                        SettingsView.Visibility = Visibility.Collapsed;
                    }

                    ViewModel.Settings.Search(SearchField.Text);
                }
            }

            UpdatePaneToggleButtonVisibility();
        }

        private void SearchReset()
        {
            //DialogsPanel.Visibility = Visibility.Visible;
            ShowHideSearch(false);

            if (rpMasterTitlebar.SelectedIndex == 0 && SearchField.FocusState != FocusState.Unfocused)
            {
                Photo.Focus(FocusState.Programmatic);
            }

            SearchField.Text = string.Empty;

            if (ContactsPanel != null)
            {
                ContactsPanel.Visibility = Visibility.Visible;
            }

            if (SettingsView != null)
            {
                SettingsView.Visibility = Visibility.Visible;
            }

            ViewModel.Contacts.Search = null;
            ViewModel.Settings.Results.Clear();
        }

        #endregion

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Passcode.Lock();
        }

        private void DialogsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.Item is SearchResult result)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ProfileCell;
                if (content == null)
                {
                    return;
                }

                args.ItemContainer.Tag = result.Chat;
                content.UpdateSearchResult(_clientService, args, DialogsSearchListView_ContainerContentChanging);
            }

            args.Handled = true;
        }

        private void UsersListView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void UsersListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as ProfileCell;

            if (args.Item is ActiveStoriesViewModel activeStories)
            {
                content.UpdateActiveStories(ViewModel.ClientService, activeStories, args, UsersListView_ContainerContentChanging);
            }
            else if (args.Item is User user)
            {
                content.UpdateUser(ViewModel.ClientService, user, args, UsersListView_ContainerContentChanging);
            }
        }

        private void Settings_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            string GetPath(SettingsSearchEntry item)
            {
                if (item.Parent != null)
                {
                    return GetPath(item.Parent) + " > " + item.Text;
                }

                return item.Text;
            }

            var entry = args.Item as SettingsSearchEntry;
            var button = args.ItemContainer.ContentTemplateRoot as BadgeButton;
            button.Command = ViewModel.Settings.NavigateCommand;
            button.CommandParameter = entry;

            button.Content = entry.Text;
            button.IconSource = entry.Icon;

            var icon = button.Descendants<Microsoft.UI.Xaml.Controls.AnimatedIcon>().FirstOrDefault() as UIElement;
            icon?.InvalidateMeasure();

            if (entry.Parent == null)
            {
                button.Badge = null;
                button.BadgeVisibility = Visibility.Collapsed;
            }
            else
            {
                button.Badge = GetPath(entry.Parent);
                button.BadgeVisibility = Visibility.Visible;
            }

            args.Handled = true;
        }

        private void Calls_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as CallCell;
            var call = args.Item as TLCallGroup;

            args.ItemContainer.Tag = call;
            content.Tag = call;

            content.UpdateCall(ViewModel.ClientService, call);
        }

        private void SetPivotIndex(int index)
        {
            if (rpMasterTitlebar.SelectedIndex != index)
            {
                rpMasterTitlebar.IsLocked = false;
                rpMasterTitlebar.SelectedIndex = index;

                if (MasterDetail.CurrentState == MasterDetailState.Minimal &&
                    MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage))
                {
                    MasterDetail.NavigationService.GoBackAt(0);
                }
            }
        }

        public async void NavigationView_ItemClick(RootDestination destination)
        {
            if (destination == RootDestination.Chats)
            {
                SetPivotIndex(0);
            }
            else if (destination == RootDestination.Contacts)
            {
                SetPivotIndex(1);
            }
            else if (destination == RootDestination.Calls)
            {
                SetPivotIndex(2);
            }
            else if (destination == RootDestination.Settings)
            {
                SetPivotIndex(3);
            }
            else if (destination == RootDestination.ArchivedChats)
            {
                ArchivedChats_Click(null, null);
            }
            else if (destination == RootDestination.Status)
            {
                Status_Click(null, null);
            }
            else if (destination == RootDestination.MyStories)
            {
                MasterDetail.NavigationService.Navigate(typeof(ChatStoriesPage));
            }
            else if (destination == RootDestination.SavedMessages)
            {
                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(ViewModel.ClientService.Options.MyId, false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                }
            }
            else if (destination == RootDestination.Tips && Uri.TryCreate(Strings.TelegramFeaturesUrl, UriKind.Absolute, out Uri tipsUri))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, MasterDetail.NavigationService, tipsUri);
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ClientService, MasterDetail.NavigationService, "unigram", null, null);
            }
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            var scrollViewer = ChatsList.GetScrollViewer();
            scrollViewer?.ChangeView(null, 0, null);
        }

        private void Proxy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsProxyPage));
        }

        private void ChatsNearby_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(ChatsNearbyPage));
        }

        private ChatFolderViewModel ConvertFolder(ChatFolderViewModel folder)
        {
            ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0 && folder.ChatList is not ChatListArchive);
            ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0);
            ShowHideArchive(folder?.ChatList is ChatListMain or null && ViewModel.Chats.Items.ChatList is not ChatListArchive);

            UpdatePaneToggleButtonVisibility();

            if (rpMasterTitlebar.SelectedIndex != 0)
            {
                SetPivotIndex(0);
            }

            ChatsList.CanGoNext = ViewModel.Folders.Count > 0 && ViewModel.Folders[^1] != folder;
            ChatsList.CanGoPrev = ViewModel.Folders.Count > 0 && ViewModel.Folders[0] != folder;

            return folder;
        }

        private void ConvertFolderBack(object obj)
        {
            if (obj is ChatFolderViewModel folder && !folder.IsNavigationItem && ViewModel.Chats.Items.ChatList is not ChatListArchive)
            {
                UpdateFolder(folder);
            }
        }

        public void ArchivedChats_Click(object sender, RoutedEventArgs e)
        {
            UpdateFolder(ChatFolderViewModel.Archive);
        }

        private void ChatFolder_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var folder = ChatFolders?.ItemFromContainer(sender) as ChatFolderViewModel;

            folder ??= ChatFoldersSide.ItemFromContainer(sender) as ChatFolderViewModel;

            if (folder.IsNavigationItem)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (folder.ChatFolderId == Constants.ChatListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.EditFolder, folder, Strings.FilterEditAll, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, folder, Strings.MarkAllAsRead, Icons.MarkAsRead);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.EditFolder, folder, Strings.FilterEdit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, folder, Strings.MarkAllAsRead, Icons.MarkAsRead);
                flyout.CreateFlyoutItem(ViewModel.AddToFolder, folder, Strings.FilterAddChats, Icons.Add);
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.DeleteFolder, folder, Strings.Remove, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(element, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        private void ArchivedChats_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            //if (((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats)
            //{
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.AccDescrExpandPanel, Icons.Expand);
            //}
            //else
            //{
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.AccDescrCollapsePanel, Icons.Collapse);
            //}

            flyout.CreateFlyoutItem(ToggleArchive, Strings.ArchiveMoveToMainMenu, Icons.SubtractCircle);
            flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, ChatFolderViewModel.Archive, Strings.MarkAllAsRead, Icons.MarkAsRead);

            args.ShowAt(flyout, element);
        }

        public async void ToggleArchive()
        {
            ViewModel.ToggleArchive();

            ArchivedChatsPanel.Visibility = Visibility.Visible;
            //ArchivedChatsCompactPanel.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            void ToggleActiveCompleted()
            {
                ArchivedChatsPanel.Visibility = ((ViewModelBase)ViewModel).Settings.HideArchivedChats
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);

                Root.UpdateSessions();

                if (((ViewModelBase)ViewModel).Settings.HideArchivedChats)
                {
                    Window.Current.ShowToast(Photo, Strings.ArchiveMoveToMainMenuInfo, TeachingTipPlacementMode.BottomRight);
                }
            }

            var show = !((ViewModelBase)ViewModel).Settings.HideArchivedChats;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ToggleActiveCompleted();
            }

            var presenter = ElementCompositionPreview.GetElementVisual(ArchivedChatsPresenter);
            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var chats = ElementCompositionPreview.GetElementVisual(element);
            var panel = ElementCompositionPreview.GetElementVisual(ArchivedChatsPanel);
            //var compact = ElementCompositionPreview.GetElementVisual(ArchivedChatsCompactPanel);

            presenter.Clip = chats.Compositor.CreateInsetClip();
            parent.Clip = chats.Compositor.CreateInsetClip();

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                panel.Offset = new Vector3();
                //compact.Offset = new Vector3();

                ToggleActiveCompleted();
            };

            var panelY = ArchivedChatsPanel.ActualSize.Y;
            var compactY = 0; //(float)ArchivedChatsCompactPanel.ActualHeight;

            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -(panelY - compactY));

            float y0, y1;

            if (show)
            {
                y0 = -(panelY - compactY);
                y1 = 0;
            }
            else
            {
                y0 = 0;
                y1 = -(panelY - compactY);
            }

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, y0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, y1, 0));
            chats.StartAnimation("Offset", offset0);

            //var offset1 = chats.Compositor.CreateVector3KeyFrameAnimation();
            //offset1.InsertKeyFrame(0, new Vector3(0, show ? 0 : compactY, 0));
            //offset1.InsertKeyFrame(1, new Vector3(0, show ? compactY : 0, 0));
            //compact.StartAnimation("Offset", offset1);

            var offset2 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(0, show ? -compactY : 0, 0));
            offset2.InsertKeyFrame(1, new Vector3(0, show ? 0 : -compactY, 0));
            panel.StartAnimation("Offset", offset2);

            batch.End();
        }

        private bool _archiveCollapsed;

        private async void ShowHideArchive(bool show)
        {
            if (_archiveCollapsed != show)
            {
                return;
            }

            _archiveCollapsed = !show;
            ArchivedChatsPresenter.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            void ShowHideArchiveCompleted()
            {
                ArchivedChatsPresenter.Visibility = _archiveCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideArchiveCompleted();
                return;
            }

            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);
            var chats = ElementCompositionPreview.GetElementVisual(element);

            parent.Clip = chats.Compositor.CreateInsetClip();
            chats.StopAnimation("Offset");

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);

                ShowHideArchiveCompleted();
            };

            var y = ArchivedChatsPresenter.ActualSize.Y;

            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -y);

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, show ? -y : 0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, show ? 0 : -y, 0));
            chats.StartAnimation("Offset", offset0);

            batch.End();
        }

        private bool _shouldGoBackWithDetail = true;

        public void BackRequested()
        {
            if (_shouldGoBackWithDetail && MasterDetail.NavigationService.CanGoBack)
            {
                BootStrapper.Current.RaiseBackRequested();
            }
            else
            {
                _shouldGoBackWithDetail = true;
                OnBackRequested(new BackRequestedRoutedEventArgs());
            }
        }

        private void UpdateFolder(ChatFolderViewModel folder, bool update = true)
        {
            ViewModel.SelectedFolder = folder;

            if (folder.ChatList is ChatListArchive)
            {
                _shouldGoBackWithDetail = false;
            }

            if (update)
            {
                ConvertFolder(folder);
            }

            Search_LostFocus(null, null);
        }

        #region Selection

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ShowHideManagePanel(true);
            }
            else
            {
                ShowHideManagePanel(false);
            }

            UpdatePaneToggleButtonVisibility();
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

            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            //manage.Offset = new Vector3(show ? -20 : 12, 8, 0);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                //manage.Offset = new Vector3(show ? 12 : -20, 8, 0);
                manage.Opacity = show ? 1 : 0;

                if (show)
                {
                    ManagePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    ManagePanel.Visibility = Visibility.Collapsed;
                    ViewModel.Chats.SelectedItems.Clear();
                }
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

            if (show)
            {
                ManagePanel.Visibility = Visibility.Visible;
            }
            else
            {
                MainHeader.Visibility = Visibility.Visible;
            }
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                ViewModel.Chats.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                    ? ListViewSelectionMode.None
                    : ListViewSelectionMode.Single;
            }
        }

        public void SetSelectionMode(bool enabled)
        {
            if (enabled)
            {
                ViewModel.Chats.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                    ? ListViewSelectionMode.None
                    : ListViewSelectionMode.Single;
            }
        }

        public async void SetSelectedItem(Chat chat)
        {
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                await System.Threading.Tasks.Task.Delay(100);
                ChatsList.SelectedItem = chat;
            }
        }

        public void SetSelectedItems(IList<Chat> chats)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                foreach (var item in chats)
                {
                    if (!ChatsList.SelectedItems.Contains(item))
                    {
                        ChatsList.SelectedItems.Add(item);
                    }
                }

                foreach (Chat item in ChatsList.SelectedItems)
                {
                    if (!chats.Contains(item))
                    {
                        ChatsList.SelectedItems.Remove(item);
                    }
                }
            }
        }

        private void UpdateManage()
        {
            if (ViewModel.Chats.SelectedItems.Count > 0)
            {
                var muted = ViewModel.Chats.SelectedItems.Any(x => ViewModel.ClientService.Notifications.GetMutedFor(x) > 0);
                ManageMute.Glyph = muted ? Icons.Alert : Icons.AlertOff;
                Automation.SetToolTip(ManageMute, muted ? Strings.UnmuteNotifications : Strings.MuteNotifications);

                var unread = ViewModel.Chats.SelectedItems.Any(x => x.IsUnread());
                ManageMark.Icon = MenuFlyoutHelper.CreateIcon(unread ? Icons.MarkAsRead : Icons.MarkAsUnread);
                ManageMark.Text = unread ? Strings.MarkAsRead : Strings.MarkAsUnread;

                ManageClear.IsEnabled = ViewModel.Chats.SelectedItems.All(x => DialogClear_Loaded(x));
            }
        }

        #endregion

        private void Confetti_Completed(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() =>
            {
                UnloadObject(Confetti);
            });
        }

        public static string GetFolderIcon(ChatListFolderFlags folder)
        {
            if (folder == ChatListFolderFlags.ExcludeMuted)
            {
                return Icons.Alert;
            }
            else if (folder == ChatListFolderFlags.ExcludeRead)
            {
                return Icons.MarkAsUnread; //FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
            }
            else if (folder == ChatListFolderFlags.ExcludeArchived)
            {
                return Icons.Archive;
            }
            else if (folder == ChatListFolderFlags.IncludeContacts)
            {
                return Icons.Person;
            }
            else if (folder == ChatListFolderFlags.IncludeNonContacts)
            {
                return Icons.PersonQuestionMark;
            }
            else if (folder == ChatListFolderFlags.IncludeGroups)
            {
                return Icons.People;
            }
            else if (folder == ChatListFolderFlags.IncludeChannels)
            {
                return Icons.Megaphone;
            }
            else if (folder == ChatListFolderFlags.IncludeBots)
            {
                return Icons.Bot;
            }

            return null;
        }

        private void ChatFolders_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var list = sender as TopNavView;
            if (list == null)
            {
                return;
            }

            if (e.Items.Count > 1 || (e.Items[0] is ChatFolderViewModel folder && folder.ChatList is ChatListMain && !_clientService.IsPremium))
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var minimum = _clientService.IsPremium ? 2 : 3;

                var items = ViewModel?.Folders;
                if (items == null || items.Count < minimum)
                {
                    list.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    list.CanReorderItems = true;
                }
            }
        }

        private void ChatFolders_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ChatFolderViewModel folder)
            {
                var items = ViewModel?.Folders;
                var index = items.IndexOf(folder);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare.ChatList is ChatListMain && index > 0 && !_clientService.IsPremium)
                {
                    compare = items[index + 1];
                }

                if (compare.ChatList is ChatListMain && !_clientService.IsPremium)
                {
                    ViewModel.Handle(new UpdateChatFolders(ViewModel.ClientService.ChatFolders, 0));
                }
                else
                {
                    var folders = items.Where(x => x.ChatList is ChatListFolder).Select(x => x.ChatFolderId).ToArray();
                    var main = _clientService.IsPremium ? items.IndexOf(items.FirstOrDefault(x => x.ChatList is ChatListMain)) : 0;

                    ViewModel.ClientService.Send(new ReorderChatFolders(folders, main));
                }
            }
        }

        private void ChatFolders_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatFolderViewModel folder)
            {
                var index = int.MaxValue - folder.ChatFolderId;
                if (index >= 0 && index <= 3)
                {
                    SetPivotIndex(index);
                }
                else
                {
                    SetPivotIndex(0);
                }
            }

            if (MasterDetail.CurrentState == MasterDetailState.Minimal &&
                MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage))
            {
                MasterDetail.NavigationService.GoBackAt(0);
            }
            else
            {
                var scrollingHost = ChatsList.GetScrollViewer();
                scrollingHost?.ChangeView(null, 0, null);
            }

            ShowHideTopicList(false);
        }

        private void ArchivedChats_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        private async void Downloads_Click(object sender, RoutedEventArgs e)
        {
            await new DownloadsPopup(ViewModel.SessionId, ViewModel.NavigationService).ShowQueuedAsync();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }
            else if (!_topicListCollapsed)
            {
                ShowHideTopicList(false);
                UpdateListViewsSelectedItem(0);
            }
            else if (rpMasterTitlebar.SelectedIndex > 0)
            {
                SetPivotIndex(0);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
            }
            else if (ViewModel.Chats.Items.ChatList is ChatListArchive)
            {
                UpdateFolder(ViewModel.Folders.Count > 0 ? ViewModel.Folders[0] : ChatFolderViewModel.Main);
            }
            else
            {
                Root.IsPaneOpen = true;
            }
        }

        private void ChatsList_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            // ListViewBase ignores GettingFocus events with Direction equals to None
            // What we do here is to simulate the default behavior, so that closing the active chat
            // will move the focus to the last selected item in the chat list if possible.
            if (args.Direction == FocusNavigationDirection.None && args.OldFocusedElement is not ChatListListViewItem)
            {
                if (ChatsList.TryGetContainer(ViewModel.Chats.LastSelectedItem, out SelectorItem container))
                {
                    args.TrySetNewFocusedElement(container);
                    args.Handled = true;
                }
                else if (sender != ChatsList)
                {
                    args.TrySetNewFocusedElement(ChatsList);
                    args.Handled = true;
                }
            }
        }

        private void ChatFolders_ChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
        {
            args.GroupHeaderContainer = new ListViewHeaderItem
            {
                Visibility = args.GroupIndex == 0
                    ? Visibility.Collapsed
                    : Visibility.Visible
            };
        }

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(BasicGroupCreateStep1Page));
        }

        private void NewChannel_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(ChannelCreateStep1Page));
        }

        private void ChatsList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatListListViewItem(ChatsList);
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        public void PopupOpened()
        {
            Window.Current.SetTitleBar(null);

            if (MasterDetail.NavigationService.Frame.Content is IActivablePage page)
            {
                page.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            Window.Current.SetTitleBar(TitleBarHandle);

            if (MasterDetail.NavigationService.Frame.Content is IActivablePage page)
            {
                page.PopupClosed();
            }
        }

        #region Context menu


        private void DialogsSearchPanel_ItemContextRequested(UIElement sender, ItemContextRequestedEventArgs args)
        {
            if (args.Item is SearchResult result && result.Chat != null)
            {
                var element = sender as FrameworkElement;
                var chat = result.Chat;

                Chat_ContextRequested(chat, element, args.EventArgs, false);
            }
        }

        private void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            Chat_ContextRequested(chat, sender, args, true);
        }

        private async void Chat_ContextRequested(Chat chat, UIElement sender, ContextRequestedEventArgs args, bool allowSelection)
        {
            var viewModel = ViewModel.Chats;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            var position = chat?.GetPosition(viewModel.Items.ChatList);
            if (position == null)
            {
                return;
            }

            var muted = ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0;
            var archived = chat.Positions.Any(x => x.List is ChatListArchive);

            flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ArchiveChat, chat, archived ? Strings.Unarchive : Strings.Archive, archived ? Icons.Unarchive : Icons.Archive);
            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.PinChat, chat, position.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, position.IsPinned ? Icons.PinOff : Icons.Pin);

            if (viewModel.Items.ChatList is ChatListFolder chatListFolder)
            {
                flyout.CreateFlyoutItem(viewModel.RemoveFromFolder, (chatListFolder.ChatFolderId, chat), Strings.FilterRemoveFrom, Icons.FolderMove);
            }
            else
            {
                var response = await ViewModel.ClientService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
                if (response != null && response.ChatListsValue.Count > 0)
                {
                    var folders = ViewModel.ClientService.ChatFolders;

                    var item = new MenuFlyoutSubItem();
                    item.Text = Strings.FilterAddTo;
                    item.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);

                    foreach (var chatList in response.ChatListsValue.OfType<ChatListFolder>())
                    {
                        var folder = folders.FirstOrDefault(x => x.Id == chatList.ChatFolderId);
                        if (folder != null)
                        {
                            var icon = Icons.ParseFolder(folder.Icon);
                            var glyph = Icons.FolderToGlyph(icon);

                            item.CreateFlyoutItem(viewModel.AddToFolder, (folder.Id, chat), folder.Title, glyph.Item1);
                        }
                    }

                    if (folders.Count < 10 && item.Items.Count > 0)
                    {
                        item.CreateFlyoutSeparator();
                        item.CreateFlyoutItem(viewModel.CreateFolder, chat, Strings.CreateNewFilter, Icons.Add);
                    }

                    if (item.Items.Count > 0)
                    {
                        flyout.Items.Add(item);
                    }
                }
            }

            if (DialogNotify_Loaded(chat))
            {
                var silent = chat.DefaultDisableNotification;

                var mute = new MenuFlyoutSubItem();
                mute.Text = Strings.Mute;
                mute.Icon = MenuFlyoutHelper.CreateIcon(muted ? Icons.Alert : Icons.AlertOff);

                if (muted is false)
                {
                    mute.CreateFlyoutItem(true, () => { },
                        silent ? Strings.SoundOn : Strings.SoundOff,
                        silent ? Icons.MusicNote2 : Icons.MusicNoteOff2);
                }

                mute.CreateFlyoutItem(ViewModel.Chats.MuteChatFor, Tuple.Create<Chat, int?>(chat, 60 * 60), Strings.MuteFor1h, Icons.ClockAlarmHour);
                mute.CreateFlyoutItem(ViewModel.Chats.MuteChatFor, Tuple.Create<Chat, int?>(chat, null), Strings.MuteForPopup, Icons.AlertSnooze);

                var toggle = mute.CreateFlyoutItem(
                    ViewModel.Chats.NotifyChat,
                    chat,
                    muted ? Strings.UnmuteNotifications : Strings.MuteNotifications,
                    muted ? Icons.Speaker3 : Icons.SpeakerOff);

                if (muted is false)
                {
                    toggle.Foreground = BootStrapper.Current.Resources["DangerButtonBackground"] as Brush;
                }

                flyout.Items.Add(mute);

            }

            flyout.CreateFlyoutItem(DialogMark_Loaded, viewModel.MarkChatAsRead, chat, chat.IsUnread() ? Strings.MarkAsRead : Strings.MarkAsUnread, chat.IsUnread() ? Icons.MarkAsRead : Icons.MarkAsUnread);
            flyout.CreateFlyoutItem(DialogClear_Loaded, viewModel.ClearChat, chat, Strings.ClearHistory, Icons.Broom);
            flyout.CreateFlyoutItem(DialogDelete_Loaded, viewModel.DeleteChat, chat, DialogDelete_Text(chat), Icons.Delete, destructive: true);

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.OpenChat, chat, Strings.OpenInNewWindow, Icons.WindowNew);

                if (allowSelection)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.SelectChat, chat, Strings.Select, Icons.CheckmarkCircle);
                }
            }

            args.ShowAt(flyout, element);
        }

        private bool DialogMark_Loaded(Chat chat)
        {
            return true;
        }

        private bool DialogPin_Loaded(Chat chat)
        {
            //if (!chat.IsPinned)
            //{
            //    var count = ViewModel.Dialogs.LegacyItems.Where(x => x.IsPinned).Count();
            //    var max = ViewModel.ClientService.Config.PinnedDialogsCountMax;

            //    return count < max ? Visibility.Visible : Visibility.Collapsed;
            //}

            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            return true;
        }

        private bool DialogArchive_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (ViewModel.ClientService.IsSavedMessages(chat) || position?.Source != null || chat.Id == 777000)
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (ViewModel.ClientService.IsSavedMessages(chat) || position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return false;
            }

            return true;
        }

        public bool DialogClear_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return !supergroup.HasActiveUsername() && !super.IsChannel;
                }
            }

            return true;
        }

        private bool DialogDelete_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return false;
            }

            //if (dialog.With is TLChannel channel)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerUser userPeer)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerChat chatPeer)
            //{
            //    return dialog.With is TLChatForbidden || dialog.With is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed;
            //}

            //return Visibility.Collapsed;

            return true;
        }

        private string DialogDelete_Text(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return Strings.PsaHide;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.LeaveChannelMenu : Strings.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.DeleteAndExit;
            }

            return Strings.Delete;
        }

        #endregion

        private void Status_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsPremium)
            {
                EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.CustomEmojis, TitleBarLogo, EmojiFlyoutAlignment.TopLeft);
            }
        }

        private void TopicList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatListListViewItem(ChatsList);
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Topic_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        #region Context menu

        private void Topic_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel.Topics;
            var chat = viewModel?.Chat;

            if (viewModel == null || !viewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var topic = TopicList.ItemFromContainer(element) as ForumTopic;

            var canManage = CanManageTopic(chat, supergroup, topic);

            if (canManage && topic.Info.IsGeneral)
            {
                flyout.CreateFlyoutItem(viewModel.HideTopic, topic, topic.Info.IsHidden ? Strings.UnhideFromTop : Strings.HideOnTop, topic.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            if (canManage)
            {
                flyout.CreateFlyoutItem(viewModel.PinTopic, topic, topic.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, topic.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            var muted = ViewModel.ClientService.Notifications.GetMutedFor(chat, topic) > 0;
            flyout.CreateFlyoutItem(viewModel.NotifyTopic, topic, muted ? Strings.Unmute : Strings.Mute, topic.IsPinned ? Icons.Alert : Icons.AlertOff);

            if (canManage)
            {
                flyout.CreateFlyoutItem(viewModel.CloseTopic, topic, topic.Info.IsClosed ? Strings.RestartTopic : Strings.CloseTopic, topic.Info.IsClosed ? Icons.PlayCircle : Icons.HandRight);
            }

            if (topic.UnreadCount > 0)
            {
                flyout.CreateFlyoutItem(viewModel.MarkTopicAsRead, topic, Strings.MarkAsRead, Icons.MarkAsRead);
            }

            if (canManage)
            {
                flyout.CreateFlyoutItem(viewModel.DeleteTopic, topic, Strings.Delete, Icons.Delete, destructive: true);
            }

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.OpenTopic, topic, Strings.OpenInNewWindow, Icons.WindowNew);
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.SelectTopic, topic, Strings.Select, Icons.CheckmarkCircle);
            }

            args.ShowAt(flyout, element);
        }

        private bool CanManageTopic(Chat chat, Supergroup supergroup, ForumTopic topic)
        {
            if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.Rights.CanPinMessages || supergroup.IsChannel && admin.Rights.CanEditMessages)))
            {
                return true;
            }
            else if (supergroup.Status is ChatMemberStatusRestricted restricted)
            {
                return restricted.Permissions.CanManageTopics;
            }

            return chat.Permissions.CanManageTopics;
        }

        #endregion


        private void TopicList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var topic = args.Item as ForumTopic;
            var cell = args.ItemContainer.ContentTemplateRoot as ForumTopicCell;

            cell.UpdateForumTopic(_clientService, topic, ViewModel.Topics.Chat);
        }

        private bool _topicListCollapsed = true;

        private async void ShowHideTopicList(bool show)
        {
            if (_topicListCollapsed != show)
            {
                return;
            }

            FindName(nameof(TopicListPresenter));

            _topicListCollapsed = !show;
            TopicListPresenter.Visibility = Visibility.Visible;

            if (TopicListPresenter.ActualWidth == 0)
            {
                await TopicListPresenter.UpdateLayoutAsync();
            }

            void ShowHideTopicListCompleted()
            {
                if (_topicListCollapsed)
                {
                    TopicListPresenter.Visibility = Visibility.Collapsed;
                }
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideTopicListCompleted();
                return;
            }

            var chats = ElementCompositionPreview.GetElementVisual(element);
            var panel = ElementCompositionPreview.GetElementVisual(TopicListPresenter);

            ElementCompositionPreview.SetIsTranslationEnabled(TopicListPresenter, true);

            chats.Clip ??= chats.Compositor.CreateInsetClip();

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_topicListCollapsed)
                {
                    chats.Clip = null;
                    TopicListPresenter.Visibility = Visibility.Collapsed;
                }
            };

            var x = TopicListPresenter.ActualSize.X;

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(show ? x : 0, 0, 0));
            offset0.InsertKeyFrame(1, new Vector3(show ? 0 : x, 0, 0));
            //offset0.Duration = Constants.FastAnimation;

            var clip0 = chats.Compositor.CreateScalarKeyFrameAnimation();
            clip0.InsertKeyFrame(0, show ? 0 : x);
            clip0.InsertKeyFrame(1, show ? x : 0);
            //clip0.Duration = Constants.FastAnimation;

            panel.StartAnimation("Translation", offset0);
            chats.Clip.StartAnimation("RightInset", clip0);

            ChatsList.UpdateViewState(show ? MasterDetailState.Compact : MasterDetail.CurrentState);

            batch.End();

            UpdatePaneToggleButtonVisibility();
        }

        private void ChatList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                return;
            }

            var chats = ElementCompositionPreview.GetElementVisual(element);
            if (chats.Clip is InsetClip inset && inset.RightInset != 0 && TopicListPresenter != null)
            {
                inset.RightInset = TopicListPresenter.ActualSize.X;
            }
        }

        private void Banner_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MasterDetail.BackgroundMargin = new Thickness(0, -e.NewSize.Height, 0, 0);
        }

        private void ChatListHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Stories_Expanding(object sender, EventArgs e)
        {
            if (rpMasterTitlebar.SelectedIndex != 0)
            {
                SetPivotIndex(0);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
            }
            else if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }
        }

        private void ComposeButton_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void ChatCell_StoryClick(object sender, Chat chat)
        {
            if (sender is ActiveStoriesSegments segments)
            {
                segments.Open(ViewModel.NavigationService, ViewModel.ClientService, chat, 48, story =>
                {
                    var container = ChatsList.ContainerFromItem(story.Chat) as SelectorItem;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(Window.Current.Content);
                        var point = transform.TransformPoint(new Point());

                        return new Rect(point.X + 4 + 8, point.Y + 4 + 8, 40, 40);
                    }

                    return Rect.Empty;
                });
            }
        }

        #region Chat List

        private void Chats_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is Chat chat)
                {
                    var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
                    if (position == null || !position.IsPinned || e.Items.Count > 1 || ChatsList.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        ChatsList.CanReorderItems = false;
                        e.Cancel = true;
                    }
                    else
                    {
                        ChatsList.CanReorderItems = true;
                    }
                }
            }
            catch
            {
                e.Cancel = true;
            }
        }

        private void Chats_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ChatsList.CanReorderItems = false;

            var chatList = ViewModel.Chats.Items.ChatList;
            if (chatList == null)
            {
                return;
            }

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is Chat chat)
            {
                var items = ViewModel.Chats.Items;
                if (items.Count == 1)
                {
                    return;
                }

                var index = items.IndexOf(chat);
                var compare = items[index > 0 ? index - 1 : index + 1];

                var position = compare.GetPosition(items.ChatList);
                if (position == null)
                {
                    return;
                }

                if (position.Source != null && index > 0)
                {
                    position = items[index + 1].GetPosition(items.ChatList);
                }

                if (position.IsPinned)
                {
                    var pinned = items.Where(x =>
                    {
                        var position = x.GetPosition(items.ChatList);
                        if (position == null)
                        {
                            return false;
                        }

                        return position.IsPinned;
                    }).Select(x => x.Id).ToArray();

                    ViewModel.ClientService.Send(new SetPinnedChats(chatList, pinned));
                }
                else
                {
                    var real = chat.GetPosition(items.ChatList);
                    if (real != null)
                    {
                        items.Handle(chat.Id, real.Order);
                    }
                }
            }
        }

        private void ChatsList_Swiped(object sender, ChatListSwipedEventArgs e)
        {
            ScrollFolder(e.Direction == CarouselDirection.Next ? 1 : -1, true);
        }

        #endregion
    }

    public enum HostedNavigationMode
    {
        Child,
        Root,
        RootWhenParameterless,
    }

    public class HostedPage : Page
    {
        #region HasHeader

        public bool ShowHeader
        {
            get { return (bool)GetValue(HasHeaderProperty); }
            set { SetValue(HasHeaderProperty, value); }
        }

        public static readonly DependencyProperty HasHeaderProperty =
            DependencyProperty.Register("ShowHeader", typeof(bool), typeof(HostedPage), new PropertyMetadata(true));

        #endregion

        #region Action

        public UIElement Action
        {
            get { return (UIElement)GetValue(ActionProperty); }
            set { SetValue(ActionProperty, value); }
        }

        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register("Action", typeof(UIElement), typeof(HostedPage), new PropertyMetadata(null));

        #endregion

        #region Title

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(HostedPage), new PropertyMetadata(null));

        #endregion

        #region NavigationMode

        public HostedNavigationMode NavigationMode { get; set; }

        #endregion
    }
}
