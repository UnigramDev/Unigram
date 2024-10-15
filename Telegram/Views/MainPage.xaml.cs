//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Views;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Services.Keyboard;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Create;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Point = Windows.Foundation.Point;

namespace Telegram.Views
{
    public sealed partial class MainPage : CorePage, IRootContentPage, INavigatingPage, IChatListDelegate
    {
        private MainViewModel _viewModel;
        public MainViewModel ViewModel => _viewModel ??= DataContext as MainViewModel;

        public RootPage Root { get; set; }

        private readonly IClientService _clientService;

        private readonly DispatcherTimer _memoryUsageTimer;
        private double _memoryUsage;

        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<MainViewModel>();

            _clientService = ViewModel.ClientService;

            ViewModel.Chats.Delegate = this;
            ViewModel.PlaybackService.SourceChanged += OnPlaybackSourceChanged;

            InitializeSearch();
            InitializeLock();

            UpdateChatFolders();

            VisualUtilities.DropShadow(UpdateShadow);

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

            if (Constants.DEBUG)
            {
                FocusManager.GettingFocus += OnGettingFocus;
            }
        }

        private void OnGettingFocus(object sender, GettingFocusEventArgs args)
        {
            Logger.Info(string.Format("New: {0}, Old: {1}, {2}, {3} ~> {4}",
                args.NewFocusedElement?.GetType().Name ?? "null",
                args.OldFocusedElement?.GetType().Name ?? "null",
                args.Direction, args.InputDevice, args.FocusState));
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

        public INavigationService NavigationService => MasterDetail.NavigationService;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Initialize();
            NavigationService.Window.SetTitleBar(TitleBarHandle);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            NavigationService.Window.SetTitleBar(null);
        }

        public void Dispose()
        {
            try
            {
                Bindings.StopTracking();

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

                if (_memoryUsageTimer != null)
                {
                    _memoryUsageTimer.Tick -= MemoryUsageTimer_Tick;
                    _memoryUsageTimer.Stop();
                }

                if (Constants.DEBUG)
                {
                    FocusManager.GettingFocus -= OnGettingFocus;
                }
            }
            catch { }
        }

        protected override void OnLayoutMetricsChanged(SystemOverlayMetrics metrics)
        {
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(metrics.LeftInset > 0 ? 138 : 0, GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[4].Width = new GridLength(metrics.RightInset > 0 ? 138 : 0, GridUnitType.Pixel);

            Grid.SetColumn(TitleBarLogo, metrics.LeftInset > 0 ? 3 : 1);
            TitleText.FlowDirection = metrics.LeftInset > 0
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            TitleBarLogo.Margin = metrics.LeftInset > 0
                ? new Thickness(4, 0, -10, 0)
                : new Thickness(-10, 0, 4, 0);

            Photo.HorizontalAlignment = metrics.LeftInset > 0
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;

            Stories.SystemOverlayLeftInset = metrics.LeftInset > 0 ? 138 : 0;

            UpdateTitleBarMargins();
        }

        private void UpdateTitleBarMargins()
        {
            var pad = MasterDetail.MasterVisibility != Visibility.Visible || !_tabsLeftCollapsed;
            var left = pad ? 14 : 48;
            var right = pad ? -50 : 10;

            if (TitleText.FlowDirection == FlowDirection.LeftToRight)
            {
                TitleBarrr.Margin = new Thickness(left, 0, right, 0);
            }
            else
            {
                TitleBarrr.Margin = new Thickness(right, 0, left, 0);
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

        public void Handle(UpdateChatAddedToList update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatChatLists(chat));
        }

        public void Handle(UpdateChatRemovedFromList update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatChatLists(chat));
        }

        public void Handle(UpdateChatTitle update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatTitle(chat));
        }

        public void Handle(UpdateChatPhoto update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatPhoto(chat));
        }

        public void Handle(UpdateChatEmojiStatus update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatEmojiStatus(chat));
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

                    if (SetBirthdateCard != null)
                    {
                        SetBirthdateCard.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    FindName(nameof(UnconfirmedCard));
                    UnconfirmedCard.Update(update.Session);

                    if (SetBirthdateCard != null)
                    {
                        SetBirthdateCard.Visibility = Visibility.Collapsed;
                    }
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

        public void Handle(UpdateSuggestedActions update)
        {
            this.BeginOnUIThread(() =>
            {
                if (_clientService.HasSuggestedAction(new SuggestedActionSetBirthdate()))
                {
                    FindName(nameof(SetBirthdateCard));
                }
                else
                {
                    UnloadObject(SetBirthdateCard);
                }
            });
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.ExpectBlocking || update.Name == OptionsService.R.EnabledProxyId)
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
                NavigationService.Window.Title = text;
            }
            catch { }
        }

        private void HideState()
        {
            State.IsIndeterminate = false;
            StateLabel.Text = Constants.DEBUG
                ? Strings.AppName
                : "Unigram";

            try
            {
                NavigationService.Window.Title = string.Empty;
            }
            catch { }
        }

        public void Handle(UpdateActiveCall update)
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
                var call = ViewModel.VoipService.ActiveCall;
                if (call != null)
                {
                    UpdatePlaybackHidden(true);
                    FindName(nameof(CallBanner));

                    CallBanner.Update(call);
                }
                else
                {
                    UpdatePlaybackHidden(false);

                    if (CallBanner != null)
                    {
                        CallBanner.Update(null);
                        UnloadObject(CallBanner);
                    }
                }
            });
        }

        public void Handle(UpdateChatFoldersLayout update)
        {
            this.BeginOnUIThread(UpdateChatFolders);
        }

        #endregion

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

            if (TopicListPresenter != null)
            {
                var padding = ChatTabs != null
                    ? _tabsTopCollapsed ? -74 : -78
                    : -12;

                TopicListPresenter.Margin = new Thickness(68, padding, 0, 0);
            }

            Stories.TabsTopCollapsed = !show;
            Stories.ControlledList = ChatsList;

            void ShowHideTopTabsCompleted()
            {
                DialogsPanel.Margin = new Thickness();
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
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

            var topPadding = Stories.GetTopPadding(false);

            ChatTabs.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, topPadding, 0, 0);
            DialogsPanel.Margin = new Thickness(0, 0, 0, -40);

            var visual = ElementComposition.GetElementVisual(DialogsPanel);
            var header = ElementComposition.GetElementVisual(ChatTabsView);
            header.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ShowHideTopTabsCompleted();
            };

            var offset1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, -36);
            offset1.InsertKeyFrame(show ? 1 : 0, 0);
            //offset.Duration = Constants.FastAnimation;

            var offset2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, 36);
            offset2.InsertKeyFrame(show ? 1 : 0, 0);
            //offset.Duration = Constants.FastAnimation;

            header.Clip.StartAnimation("TopInset", offset2);
            visual.StartAnimation("Offset.Y", offset1);

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

            UpdateTitleBarMargins();

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

            ShowHideLeftTabsCompleted();
            return;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideLeftTabsCompleted();
                return;
            }

            ChatTabsLeft.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -40);

            var parent = ElementComposition.GetElementVisual(ChatsList);

            var visual = ElementComposition.GetElementVisual(element);
            var header = ElementComposition.GetElementVisual(ChatTabsView);

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
                HideTopicList();
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

            if (rpMasterTitlebar.SelectedIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
                args.Handled = true;
            }
            else
            {
                var scrollViewer = ChatsList.GetScrollViewer();
                if (scrollViewer != null && scrollViewer.VerticalOffset > 50)
                {
                    Logger.Info("ChangeView");

                    scrollViewer.ChangeView(null, 0, null);
                    args.Handled = true;
                }
                else if (ViewModel.Chats.Items.ChatList is ChatListArchive
                    || ViewModel.Folders.Count > 0 && !ViewModel.Chats.Items.ChatList.AreTheSame(ViewModel.Folders[0].ChatList))
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
                LogoEmoji.Visibility = Visibility.Visible;
                LogoEmoji.Source = new CustomEmojiFileSource(_clientService, user.EmojiStatus.CustomEmojiId);
            }
            else
            {
                LogoBasic.Visibility = _clientService.IsPremium ? Visibility.Collapsed : Visibility.Visible;
                LogoPremium.Visibility = _clientService.IsPremium ? Visibility.Visible : Visibility.Collapsed;
                LogoEmoji.Visibility = Visibility.Collapsed;
                LogoEmoji.Source = null;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                UpdateUser(user);
            }

            Subscribe();

            var context = WindowContext.ForXamlRoot(XamlRoot);
            if (context != null)
            {
                context.CoreWindow.CharacterReceived += OnCharacterReceived;
                context.InputListener.KeyDown += OnAcceleratorKeyActivated;
            }

            OnStateChanged(null, null);

            ShowHideBanner(ViewModel.PlaybackService.CurrentItem != null);

            var update = new UpdateConnectionState(ViewModel.ClientService.ConnectionState);
            if (update.State != null)
            {
                Handle(update);
                ViewModel.Aggregator.Publish(update);
            }

            Handle(new UpdateUnconfirmedSession(ViewModel.ClientService.UnconfirmedSession));
            UpdateChatFolders();

            if (_clientService.HasSuggestedAction(new SuggestedActionSetBirthdate()))
            {
                FindName(nameof(SetBirthdateCard));
            }

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
                .Subscribe<UpdateChatIsMarkedAsUnread>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateChatReadOutbox>(Handle)
                .Subscribe<UpdateChatUnreadMentionCount>(Handle)
                .Subscribe<UpdateChatUnreadReactionCount>(Handle)
                .Subscribe<UpdateChatAddedToList>(Handle)
                .Subscribe<UpdateChatRemovedFromList>(Handle)
                .Subscribe<UpdateChatTitle>(Handle)
                .Subscribe<UpdateChatPhoto>(Handle)
                .Subscribe<UpdateChatEmojiStatus>(Handle)
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
                .Subscribe<UpdateChatNotificationSettings>(Handle)
                .Subscribe<UpdatePasscodeLock>(Handle)
                .Subscribe<UpdateUnconfirmedSession>(Handle)
                .Subscribe<UpdateConnectionState>(Handle)
                .Subscribe<UpdateOption>(Handle)
                .Subscribe<UpdateSuggestedActions>(Handle)
                .Subscribe<UpdateActiveCall>(Handle)
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

            var detail = ElementComposition.GetElementVisual(MasterDetail.NavigationService.Frame);
            var playback = ElementComposition.GetElementVisual(Playback);

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
            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.CoreWindow.CharacterReceived -= OnCharacterReceived;
                context.InputListener.KeyDown -= OnAcceleratorKeyActivated;
            }

            Bindings.StopTracking();

            _unloaded = true;

            LeakTest(false);
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
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
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
                    //Benchmark();

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

        private async void Benchmark()
        {
            var pinned = ViewModel.Chats.Items.LastOrDefault(x => x.GetPosition(null).IsPinned);
            var index = ViewModel.Chats.Items.IndexOf(pinned);

            var next1 = ViewModel.Chats.Items[index + 1];
            var next2 = ViewModel.Chats.Items[index + 2];
            var next3 = ViewModel.Chats.Items[index + 3];

            var order = next2.GetOrder(null);

            for (int i = 0; i < 10000; i++)
            {
                if (i % 2 == 0)
                {
                    ViewModel.Chats.Items.Handle(next1.Id, order - 1);
                    ViewModel.Chats.Items.Handle(next3.Id, order + 1);
                }
                else
                {
                    ViewModel.Chats.Items.Handle(next3.Id, order - 1);
                    ViewModel.Chats.Items.Handle(next1.Id, order + 1);
                }

                await VisualUtilities.WaitForCompositionRenderingAsync();
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
                await NotifyIcon.ExitAsync();
                await BootStrapper.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Close)
            {
                await WindowContext.Current.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Lock)
            {
                Lock_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.Downloads)
            {
                Downloads_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.CallAccept && ViewModel.VoipService.ActiveCall is VoipCall acceptCall)
            {
                acceptCall.Accept(XamlRoot);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.CallReject && ViewModel.VoipService.ActiveCall is VoipCall rejectCall)
            {
                rejectCall.Discard();
                args.Handled = true;
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

                if (ViewModel.ClientService.TryGetChat(ViewModel.ClientService.Options.MyId, out Chat chat))
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
                UpdateFolder(ViewModel.Folders[index], true);
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

            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        public void Activate(string parameter)
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
                if (data.ContainsKey("chat_id") && long.TryParse(data["chat_id"], out long chatId))
                {
                    MasterDetail.NavigationService.NavigateToChat(chatId, force: false);
                }
            }

            if (XamlRoot == null)
            {
                return;
            }

            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
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
                await MasterDetail.NavigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationDataPackage(package));
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
                e.SourcePageType == typeof(ChatSavedPage) ||
                e.SourcePageType == typeof(ChatBusinessRepliesPage) ||
                e.SourcePageType == typeof(BlankPage);

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
                MasterDetail.AllowCompact = e.SourcePageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == INDEX_CHATS;
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
                frame.CurrentSourcePageType == typeof(ChatSavedPage) ||
                frame.CurrentSourcePageType == typeof(ChatBusinessRepliesPage) ||
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
            UpdateTitleBarMargins();

            Stories.IsVisible = MasterDetail.MasterVisibility == Visibility.Visible;
            Photo.Visibility = MasterDetail.MasterVisibility == Visibility.Visible || !_tabsLeftCollapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdatePaneToggleButtonVisibility()
        {
            var visible = ViewModel.Chats.Items.ChatList is ChatListArchive
                || !_searchCollapsed
                || !_topicListCollapsed
                || rpMasterTitlebar.SelectedIndex != INDEX_CHATS;

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

        private void UpdateListViewsSelectedItem(long chatId, bool fromSelection = false)
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
                else if (fromSelection)
                {
                    // If we come from selection we need to delay this as ItemClick comes before SelectionChanged,
                    // hence, if we unselect here, the ListView internal code will re-select the item right away.
                    VisualUtilities.QueueCallbackForCompositionRendered(() => ChatsList.ClearValue(Selector.SelectedItemProperty));
                }
                else
                {
                    ChatsList.ClearValue(Selector.SelectedItemProperty);
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
            if (e == null)
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
                return;
            }

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

            var profile = false;

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
                    thread: message.IsTopicMessage ? message.MessageThreadId : 0,
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

                profile = result.Type == SearchResultType.WebApps;
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

                if (chat.ViewAsTopics && chat.Type is ChatTypeSupergroup)
                {
                    if (ViewModel.Chats.SelectedItem != ViewModel.Topics.Chat?.Id)
                    {
                        ShowTopicList(chat);
                    }
                    else
                    {
                        HideTopicList(true);
                    }
                }
                else
                {
                    if (profile)
                    {
                        MasterDetail.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                    else
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false, clearBackStack: true);
                    }

                    HideTopicList();
                }
            }
            else if (item is ForumTopic topic)
            {
                ViewModel.Chats.SelectedItem = ViewModel.Topics.Chat?.Id;
                MasterDetail.NavigationService.NavigateToChat(ViewModel.Topics.Chat, thread: topic.Info.MessageThreadId);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.Chats.SelectedItems.Count > 0)
            {
                var muted = ViewModel.Chats.SelectedItems.Any(x => ViewModel.ClientService.Notifications.IsMuted(x));
                ManageMute.Glyph = muted ? Icons.Alert : Icons.AlertOff;
                Automation.SetToolTip(ManageMute, muted ? Strings.UnmuteNotifications : Strings.MuteNotifications);

                var unread = ViewModel.Chats.SelectedItems.Any(x => x.IsUnread());
                ManageMark.Icon = MenuFlyoutHelper.CreateIcon(unread ? Icons.MarkAsRead : Icons.MarkAsUnread);
                ManageMark.Text = unread ? Strings.MarkAsRead : Strings.MarkAsUnread;

                ManageClear.IsEnabled = ViewModel.Chats.SelectedItems.All(x => DialogClear_Loaded(x));
            }
        }

        private void InitializeSearch()
        {
            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                if (rpMasterTitlebar.SelectedIndex == INDEX_CHATS)
                {
                    //var items = ViewModel.Chats.Search;
                    //if (items != null && string.Equals(SearchField.Text, items.Query))
                    //{
                    //    await items.LoadMoreItemsAsync(2);
                    //    await items.LoadMoreItemsAsync(3);
                    //    await items.LoadMoreItemsAsync(4);
                    //}
                }
            };
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == INDEX_CHATS;

            switch (rpMasterTitlebar.SelectedIndex)
            {
                case INDEX_CHATS:
                    Root?.SetSelectedIndex(RootDestination.Chats);
                    break;
                case INDEX_SETTINGS:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Settings);
                    SearchField.ControlledList = null;

                    if (SettingsView == null)
                    {
                        FindName(nameof(SettingsRoot));
                        SettingsView.DataContext = ViewModel.Settings;
                        ViewModel.Settings.Delegate = SettingsView;

                        _ = ViewModel.Settings.NavigatedToAsync(null, NavigationMode.Refresh, null);
                    }
                    break;
            }

            SearchField.Text = string.Empty;

            UpdateHeader();
            UpdatePaneToggleButtonVisibility();

            SearchReset();

            if (rpMasterTitlebar.SelectedIndex != INDEX_CHATS)
            {
                Stories.Collapse();

                if (ChatFoldersSide != null)
                {
                    ChatFoldersSide.SelectedIndex = ViewModel.Folders.Count + rpMasterTitlebar.SelectedIndex - 1;
                }
            }
            else if (ChatFoldersSide != null)
            {
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
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
            if (rpMasterTitlebar.SelectedIndex == INDEX_CHATS)
            {
                ChatsOptions.Visibility = Visibility.Visible;
                SearchField.Padding = new Thickness(10, 5, 40, 6);
            }
            else
            {
                ChatsOptions.Visibility = Visibility.Collapsed;
                SearchField.Padding = new Thickness(10, 5, 6, 6);
            }

            SearchField.PlaceholderText = rpMasterTitlebar.SelectedIndex == INDEX_SETTINGS
                ? Strings.SearchInSettings
                : Strings.Search;
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
                DialogsSearchPanel.Update();
                SearchField.ControlledList = DialogsSearchPanel.Root;
                Stories.Collapse();
            }

            var chats = ElementComposition.GetElementVisual(DialogsPanel);
            var panel = ElementComposition.GetElementVisual(DialogsSearchPanel);

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
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == INDEX_CHATS;

            SearchReset();

            UpdatePaneToggleButtonVisibility();
        }

        private const int INDEX_CHATS = 0;
        private const int INDEX_SETTINGS = 1;

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrWhiteSpace(SearchField.Text))
            {
                return;
            }

            _shouldGoBackWithDetail = false;

            MasterDetail.AllowCompact = false;

            if (rpMasterTitlebar.SelectedIndex == INDEX_CHATS)
            {
                ShowHideSearch(true);
                ViewModel.SearchChats.Query = SearchField.Text;
            }
            else if (rpMasterTitlebar.SelectedIndex == INDEX_SETTINGS)
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

            if (rpMasterTitlebar.SelectedIndex == INDEX_CHATS && SearchField.FocusState != FocusState.Unfocused)
            {
                Photo.Focus(FocusState.Programmatic);
            }

            SearchField.Text = string.Empty;

            if (SettingsView != null)
            {
                SettingsView.Visibility = Visibility.Visible;
            }

            ViewModel.Settings.Results.Clear();
        }

        #endregion

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Passcode.Lock(false);
        }

        private void Settings_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            static string GetPath(SettingsSearchEntry item)
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

            var icon = button.GetChild<AnimatedIcon>();
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

        private void SetPivotSelectedIndex(int index)
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

        public void NavigationView_ItemClick(RootDestination destination)
        {
            if (destination == RootDestination.Chats)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
            }
            else if (destination == RootDestination.Contacts)
            {
                _ = ViewModel.NavigationService.ShowPopupAsync(new ContactsPopup());
            }
            else if (destination == RootDestination.Calls)
            {
                _ = ViewModel.NavigationService.ShowPopupAsync(new CallsPopup());
            }
            else if (destination == RootDestination.Settings)
            {
                SetPivotSelectedIndex(INDEX_SETTINGS);
            }
            else if (destination == RootDestination.ArchivedChats)
            {
                ArchivedChats_Click(null, null);
            }
            else if (destination == RootDestination.Status)
            {
                Status_Click(null, null);
            }
            else if (destination == RootDestination.NewGroup)
            {
                _ = ViewModel.NavigationService.ShowPopupAsync(new NewGroupPopup());
            }
            else if (destination == RootDestination.NewChannel)
            {
                _ = ViewModel.NavigationService.ShowPopupAsync(new NewChannelPopup());
            }
            else if (destination == RootDestination.MyProfile)
            {
                ViewModel.NavigateToMyProfile(false);
            }
            else if (destination == RootDestination.SavedMessages)
            {
                ViewModel.NavigateToMyProfile(true);
            }
            else if (destination == RootDestination.Tips && Uri.TryCreate(Strings.TelegramFeaturesUrl, UriKind.Absolute, out Uri tipsUri))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, MasterDetail.NavigationService, tipsUri);
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ClientService, MasterDetail.NavigationService, "unigram");
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

        public void UpdateChatListArchive()
        {
            this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive()));
        }

        public void UpdateChatFolders()
        {
            void handler(object sender, object e)
            {
                ChatsList.LayoutUpdated -= handler;
                ChatsList.ItemContainerTransitions.Clear();
            }

            ChatsList.ItemContainerTransitions.Clear();
            ChatsList.ItemContainerTransitions.Add(new RepositionThemeTransition());

            ChatsList.LayoutUpdated += handler;
            ChatsList.UpdateVisibleChats();

            ConvertFolder(ViewModel.SelectedFolder);
        }

        private ChatFolderViewModel ConvertFolder(ChatFolderViewModel folder)
        {
            ShowHideArchive(folder?.ChatList is ChatListMain or null && ViewModel.Chats.Items.ChatList is not ChatListArchive, false);
            ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0);
            ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Folders.Count > 0 && folder.ChatList is not ChatListArchive);

            UpdatePaneToggleButtonVisibility();

            if (rpMasterTitlebar.SelectedIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
            }

            ChatsList.CanGoNext = ViewModel.Folders.Count > 0 && ViewModel.Folders[^1] != folder;
            ChatsList.CanGoPrev = ViewModel.Folders.Count > 0 && ViewModel.Folders[0] != folder;

            return folder;
        }

        private void ChatFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListViewBase listView && listView.SelectedItem is ChatFolderViewModel folder)
            {
                var index = int.MaxValue - folder.ChatFolderId;
                if (index >= INDEX_CHATS && index <= INDEX_SETTINGS)
                {
                    SetPivotSelectedIndex(index);
                }
                else
                {
                    SetPivotSelectedIndex(INDEX_CHATS);

                    if (ViewModel.Chats.Items.ChatList is not ChatListArchive)
                    {
                        UpdateFolder(folder);
                    }
                }

                if (MasterDetail.CurrentState == MasterDetailState.Minimal && MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage))
                {
                    MasterDetail.NavigationService.GoBackAt(0);
                }
                else
                {
                    Logger.Info("ChangeView");

                    var scrollingHost = ChatsList.GetScrollViewer();
                    scrollingHost?.ChangeView(null, 0, null);
                }

                HideTopicList();
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

            flyout.ShowAt(sender, args);
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
                    ToastPopup.Show(Photo, Strings.ArchiveMoveToMainMenuInfo, TeachingTipPlacementMode.BottomRight);
                }
            }

            var show = !((ViewModelBase)ViewModel).Settings.HideArchivedChats;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ToggleActiveCompleted();
            }

            var presenter = ElementComposition.GetElementVisual(ArchivedChatsPresenter);
            var parent = ElementComposition.GetElementVisual(ChatsList);

            var chats = ElementComposition.GetElementVisual(element);
            var panel = ElementComposition.GetElementVisual(ArchivedChatsPanel);
            //var compact = ElementComposition.GetElementVisual(ArchivedChatsCompactPanel);

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

        private async void ShowHideArchive(bool show, bool animate)
        {
            if (_archiveCollapsed != show)
            {
                return;
            }

            _archiveCollapsed = !show;
            ArchivedChatsPresenter.Visibility = Visibility.Visible;

            void ShowHideArchiveCompleted()
            {
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
                ArchivedChatsPresenter.Visibility = _archiveCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null || !animate || ((ViewModelBase)ViewModel).Settings.HideArchivedChats)
            {
                ShowHideArchiveCompleted();
                return;
            }

            if (ArchivedChatsPanel.ActualWidth == 0)
            {
                await ArchivedChatsPanel.UpdateLayoutAsync();
            }

            var parent = ElementComposition.GetElementVisual(ChatsList);
            var chats = ElementComposition.GetElementVisual(element);

            parent.Clip = chats.Compositor.CreateInsetClip();
            chats.StopAnimation("Offset");

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
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
            CarouselDirection direction;
            if (folder.ChatList is ChatListArchive)
            {
                direction = CarouselDirection.Next;
            }
            else if (ViewModel.Chats.Items.ChatList is ChatListArchive)
            {
                direction = CarouselDirection.Previous;
            }
            else
            {
                var nextIndex = ViewModel.Folders.IndexOf(folder);
                var prevIndex = ViewModel.Folders.IndexOf(ViewModel.SelectedFolder);

                if (nextIndex == prevIndex)
                {
                    return;
                }

                direction = nextIndex <= prevIndex
                    ? CarouselDirection.Previous
                    : CarouselDirection.Next;
            }

            ChatsList.ChangeView(direction, () =>
            {
                ViewModel.SelectedFolder = folder;

                if (update)
                {
                    ConvertFolder(folder);
                }
            });

            if (folder.ChatList is ChatListArchive)
            {
                _shouldGoBackWithDetail = false;
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

            var manage = ElementComposition.GetElementVisual(ManagePanel);
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
            await System.Threading.Tasks.Task.Delay(100);

            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                try
                {
                    ChatsList.SelectedItem = chat;

                    // TODO: would be great, but doesn't seem to work well enough :(
                    //VisualUtilities.QueueCallbackForCompositionRendered(() => ChatsList.SelectedItem = chat);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public void SetSelectedItems(IList<Chat> chats)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                try
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
                catch
                {
                    // SelectedItems likes to throw
                }
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
                return Icons.AlertFilled;
            }
            else if (folder == ChatListFolderFlags.ExcludeRead)
            {
                return Icons.ChatUnreadFilled; //FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
            }
            else if (folder == ChatListFolderFlags.ExcludeArchived)
            {
                return Icons.ArchiveFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeContacts)
            {
                return Icons.PersonFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeNonContacts)
            {
                return Icons.PersonQuestionMarkFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeGroups)
            {
                return Icons.PeopleFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeChannels)
            {
                return Icons.MegaphoneFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeBots)
            {
                return Icons.BotFilled;
            }
            else if (folder == ChatListFolderFlags.ExistingChats)
            {
                return Icons.ChatMultipleFilled;
            }
            else if (folder == ChatListFolderFlags.NewChats)
            {
                return Icons.ChatUnreadFilled;
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

            if (e.Items.Count > 1)
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Folders;
                if (items == null || items.Count < 2)
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
                if (compare.ChatList is ChatListMain && index > 0 && index < items.Count - 1 && !_clientService.IsPremium)
                {
                    compare = items[index + 1];
                }

                if ((compare.ChatList is ChatListMain || folder.ChatList is ChatListMain) && !_clientService.IsPremium)
                {
                    ViewModel.Handle(new UpdateChatFolders(ViewModel.ClientService.ChatFolders, 0, false));

                    ToastPopup.ShowPromo(ViewModel.NavigationService, string.Format(Strings.LimitReachedReorderFolder, Strings.FilterAllChats), Strings.PremiumMore, new PremiumSourceLimitExceeded(new PremiumLimitTypeChatFolderCount()));
                }
                else
                {
                    var folders = items.Where(x => x.ChatList is ChatListFolder).Select(x => x.ChatFolderId).ToArray();
                    var main = _clientService.IsPremium ? items.IndexOf(items.FirstOrDefault(x => x.ChatList is ChatListMain)) : 0;

                    ViewModel.ClientService.Send(new ReorderChatFolders(folders, main));
                }
            }
        }

        private void ArchivedChats_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        private async void Downloads_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigationService.ShowPopupAsync(new DownloadsPopup(ViewModel.SessionId, ViewModel.NavigationService));
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }
            else if (!_topicListCollapsed)
            {
                HideTopicList();
            }
            else if (rpMasterTitlebar.SelectedIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
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
            try
            {
                // ListViewBase ignores GettingFocus events with Direction equals to None
                // What we do here is to simulate the default behavior, so that closing the active chat
                // will move the focus to the last selected item in the chat list if possible.
                if (args.Direction == FocusNavigationDirection.None && args.OldFocusedElement is not ChatListListViewItem)
                {
                    if (ChatsList.TryGetContainer(ViewModel?.Chats.LastSelectedItem ?? 0, out SelectorItem container))
                    {
                        if (args.TrySetNewFocusedElement(container))
                        {
                            args.Handled = true;
                        }
                    }
                    else if (sender != ChatsList)
                    {
                        if (args.TrySetNewFocusedElement(ChatsList))
                        {
                            args.Handled = true;
                        }
                    }

                    if (args.NewFocusedElement is ChatListListViewItem item)
                    {
                        // Let's disable the awkward focus rect that would appear on activation.
                        // ChatListListViewItem.OnLostFocus takes care of reenabling it.
                        item.UseSystemFocusVisuals = false;
                    }
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
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
            NavigationService.Window.SetTitleBar(null);

            if (NavigationService.Frame.Content is IActivablePage page)
            {
                page.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            NavigationService.Window.SetTitleBar(TitleBarHandle);

            if (NavigationService.Frame.Content is IActivablePage page)
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
            var chat = ChatsList.ItemFromContainer(element) as Chat;

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

            var muted = ViewModel.ClientService.Notifications.IsMuted(chat);
            var archived = chat.Positions.Any(x => x.List is ChatListArchive);

            if (DialogArchive_Loaded(chat))
            {
                // Suggest to unarchive only when archive is open
                if (viewModel.Items.ChatList is ChatListArchive && archived)
                {
                    flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ArchiveChat, chat, Strings.Unarchive);
                }
                else if (viewModel.Items.ChatList is not ChatListArchive && !archived)
                {
                    flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ArchiveChat, chat, Strings.Archive, Icons.Archive);
                }
            }

            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.PinChat, chat, position.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, position.IsPinned ? Icons.PinOff : Icons.Pin);

            var chatLists = await ViewModel.ClientService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
            if (chatLists != null && chatLists.ChatListsValue.Count > 0)
            {
                var folders = ViewModel.ClientService.ChatFolders.ToDictionary(x => x.Id);

                var item = new MenuFlyoutSubItem();
                item.Text = Strings.FilterAddTo;
                item.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);

                foreach (var chatList in chatLists.ChatListsValue.OfType<ChatListFolder>())
                {
                    // Skip current folder from "Add to folder" list to avoid confusion
                    if (chatList.AreTheSame(viewModel.Items.ChatList))
                    {
                        continue;
                    }

                    if (folders.TryGetValue(chatList.ChatFolderId, out ChatFolderInfo folder))
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

            if (viewModel.Items.ChatList is ChatListFolder chatListFolder)
            {
                var response = await ViewModel.ClientService.SendAsync(new GetChatFolder(chatListFolder.ChatFolderId)) as ChatFolder;
                if (response != null)
                {
                    response.IncludedChatIds.Remove(chat.Id);

                    if (response.Any())
                    {
                        flyout.CreateFlyoutItem(viewModel.RemoveFromFolder, (chatListFolder.ChatFolderId, chat), Strings.FilterRemoveFrom, Icons.FolderMove);
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
                if (ApiInfo.HasMultipleViews)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.OpenChat, chat, Strings.OpenInNewWindow, Icons.WindowNew);
                }

                if (allowSelection)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.SelectChat, chat, Strings.Select, Icons.CheckmarkCircle);
                }
            }

            flyout.ShowAt(sender, args);
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
                EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.EmojiStatus, LogoEmoji, EmojiFlyoutAlignment.TopLeft);
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
            var topic = TopicList.ItemFromContainer(sender) as ForumTopic;

            var canManage = CanCreateTopics(chat, supergroup, topic);

            if (canManage && topic.Info.IsGeneral)
            {
                flyout.CreateFlyoutItem(viewModel.HideTopic, topic, topic.Info.IsHidden ? Strings.UnhideFromTop : Strings.HideOnTop, topic.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            if (canManage)
            {
                flyout.CreateFlyoutItem(viewModel.PinTopic, topic, topic.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, topic.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            var muted = ViewModel.ClientService.Notifications.GetMuteFor(chat, topic) > 0;
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

            flyout.ShowAt(sender, args);
        }

        private bool CanCreateTopics(Chat chat, Supergroup supergroup, ForumTopic topic)
        {
            if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.Rights.CanPinMessages || supergroup.IsChannel && admin.Rights.CanEditMessages)))
            {
                return true;
            }
            else if (supergroup.Status is ChatMemberStatusRestricted restricted)
            {
                return restricted.Permissions.CanCreateTopics;
            }

            return chat.Permissions.CanCreateTopics;
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
            args.Handled = true;
        }

        private void ShowTopicList(Chat chat)
        {
            ViewModel.Topics.SetFilter(chat);
            ShowHideTopicList(true);
        }

        private void HideTopicList(bool fromSelection = false)
        {
            var chatId = ViewModel.Topics.Chat?.Id;

            ShowHideTopicList(false);

            if (ViewModel.Chats.SelectedItem == chatId)
            {
                UpdateListViewsSelectedItem(0, fromSelection);
            }
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

            if (show)
            {
                Stories.Collapse();
            }
            else
            {
                ViewModel.Topics.SetFilter(null);
            }

            var padding = ChatTabs != null
                ? _tabsTopCollapsed ? -74 : -78
                : -12;

            TopicListPresenter.Margin = new Thickness(68, padding, 0, 0);

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

            var scrollingHost = VisualTreeHelper.GetChild(element, 1) as UIElement;

            var chats = ElementComposition.GetElementVisual(element);
            var panel = ElementComposition.GetElementVisual(TopicListPresenter);

            var compositor = chats.Compositor;

            var inset = 68;
            var width = ChatsList.ActualSize.X - inset;

            var sourceOffset = new Vector2(inset, 0);
            var sourceSize = new Vector2(width, ChatsList.ActualSize.Y);

            var redirect = compositor.CreateRedirectVisual(scrollingHost, sourceOffset, sourceSize);
            redirect.Offset = new Vector3(sourceOffset, 0);
            redirect.Clip = compositor.CreateInsetClip();

            ElementCompositionPreview.SetElementChildVisual(ChatsList, redirect);
            ElementCompositionPreview.SetIsTranslationEnabled(TopicListPresenter, true);

            chats.Clip = compositor.CreateInsetClip(0, 0, width, 0);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                redirect.Size = Vector2.Zero;
                ElementCompositionPreview.SetElementChildVisual(ChatsList, null);

                if (_topicListCollapsed)
                {
                    chats.Clip = null;
                    TopicListPresenter.Visibility = Visibility.Collapsed;
                }
            };

            var offset0 = compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(show ? width : 0, 0, 0));
            offset0.InsertKeyFrame(1, new Vector3(show ? 0 : width, 0, 0));
            //offset0.Duration = Constants.FastAnimation;

            var offset1 = compositor.CreateScalarKeyFrameAnimation();
            offset1.InsertKeyFrame(0, show ? inset : -width + inset);
            offset1.InsertKeyFrame(1, show ? -width + inset : inset);
            //offset0.Duration = Constants.FastAnimation;

            var clip0 = compositor.CreateScalarKeyFrameAnimation();
            clip0.InsertKeyFrame(0, show ? 0 : width);
            clip0.InsertKeyFrame(1, show ? width : 0);
            //clip0.Duration = Constants.FastAnimation;

            panel.StartAnimation("Translation", offset0);
            redirect.StartAnimation("Offset.X", offset1);
            redirect.Clip.StartAnimation("LeftInset", clip0);

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

            var chats = ElementComposition.GetElementVisual(element);
            if (chats.Clip is InsetClip inset && inset.RightInset != 0 && TopicListPresenter != null)
            {
                inset.RightInset = TopicListPresenter.ActualSize.X;
            }
        }

        private void Banner_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MasterDetail.BackgroundMargin = new Thickness(0, -e.NewSize.Height, 0, 0);
        }

        private void Stories_Expanding(object sender, EventArgs e)
        {
            if (rpMasterTitlebar.SelectedIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
            }
            else if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }

            HideTopicList();
        }

        private void ComposeButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ViewModel.NavigationService.ShowPopupAsync(new ContactsPopup
            {
                Title = Strings.NewMessageTitle
            });
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
                        var transform = container.TransformToVisual(null);
                        var point = transform.TransformPoint(new Point());

                        return new Rect(point.X + 4 + 8, point.Y + 4 + 8, 40, 40);
                    }

                    return Rect.Empty;
                });
            }
        }

        public Task UpdateLayoutAsync()
        {
            if (ChatsList.IsConnected)
            {
                if (ChatsList.ItemsPanelRoot != null)
                {
                    return ChatsList.ItemsPanelRoot.UpdateLayoutAsync();
                }

                return ChatsList.UpdateLayoutAsync();
            }

            return Task.CompletedTask;
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

        private bool _testLeak;

        [Conditional("DEBUG")]
        public void LeakTest(bool enable)
        {
            if (!_testLeak)
            {
                if (enable)
                {
                    _testLeak = true;
                }

                return;
            }

            return;

            _viewModel = null;
            LayoutRoot.Children.Clear();

            NavigationResults = null;
            LayoutRoot = null;
            State = null;
            TitleBarrr = null;
            ChatTabsLeft = null;
            Photo = null;
            MasterDetail = null;
            Confetti = null;
            Playback = null;
            CallBanner = null;
            Header = null;
            rpMasterTitlebar = null;
            Stories = null;
            SettingsRoot = null;
            SettingsView = null;
            DialogsSearchPanel = null;
            DialogsPanel = null;
            ChatsPanel = null;
            Downloads = null;
            ManagePanel = null;
            ButtonManage = null;
            ManageCount = null;
            ManageMute = null;
            ManageMark = null;
            ManageClear = null;
            UpdateShadow = null;
            UpdateCloud = null;
            ChatListHeader = null;
            ChatsList = null;
            TopicListPresenter = null;
            EmptyState = null;
            TopicList = null;
            ArchivedChatsPresenter = null;
            ArchivedChatsPanel = null;
            ArchivedChats = null;
            SetBirthdateCard = null;
            UnconfirmedCard = null;
            ChatTabs = null;
            ChatTabsView = null;
            ChatFolders = null;
            MainHeader = null;
            SearchField = null;
            ChatsOptions = null;
            Proxy = null;
            Lock = null;
            ChatFoldersSide = null;
            TitleBarHandle = null;
            TitleBarLogo = null;
            TitleText = null;
            StateLabel = null;
            MemoryLabel = null;
            LogoBasic = null;
            LogoPremium = null;
            LogoEmoji = null;
        }
    }
}
