using LinqToVisualTree;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Controls.Chats;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.Views.BasicGroups;
using Unigram.Views.Channels;
using Unigram.Views.Host;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Unigram.Views.Users;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class MainPage : Page
        , IRootContentPage
        , INavigatingPage
        , IChatListDelegate
    //, IHandle<UpdateFileDownloads>
    //, IHandle<UpdateChatPosition>
    //, IHandle<UpdateChatIsMarkedAsUnread>
    //, IHandle<UpdateChatReadInbox>
    //, IHandle<UpdateChatReadOutbox>
    //, IHandle<UpdateChatUnreadMentionCount>
    //, IHandle<UpdateChatUnreadReactionCount>
    //, IHandle<UpdateChatTitle>
    //, IHandle<UpdateChatPhoto>
    //, IHandle<UpdateChatVideoChat>
    //, IHandle<UpdateUserStatus>
    //, IHandle<UpdateUser>
    //, IHandle<UpdateChatAction>
    //, IHandle<UpdateMessageMentionRead>
    //, IHandle<UpdateMessageUnreadReactions>
    //, IHandle<UpdateUnreadChatCount>
    //, //, IHandle<UpdateMessageContent>
    //, IHandle<UpdateSecretChat>
    //, IHandle<UpdateChatFilters>
    //, IHandle<UpdateChatNotificationSettings>
    //, IHandle<UpdatePasscodeLock>
    //, IHandle<UpdateConnectionState>
    //, IHandle<UpdateOption>
    //, IHandle<UpdateCallDialog>
    //, IHandle<UpdateChatFiltersLayout>
    //, IHandle<UpdateConfetti>
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        public RootPage Root { get; set; }

        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;

        private readonly AnimatedListHandler _handler;

        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MainViewModel>();

            _protoService = ViewModel.ProtoService;
            _cacheService = ViewModel.CacheService;

            _handler = new AnimatedListHandler(ChatsList);

            ViewModel.Chats.Delegate = this;
            ViewModel.PlaybackService.PropertyChanged += OnCurrentItemChanged;

            NavigationCacheMode = NavigationCacheMode.Disabled;

            InitializeTitleBar();
            InitializeLocalization();
            InitializeSearch();
            InitializeLock();

            var update = new UpdateConnectionState(ViewModel.CacheService.GetConnectionState());
            if (update.State != null)
            {
                Handle(update);
                ViewModel.Aggregator.Publish(update);
            }

            DropShadowEx.Attach(UpdateShadow);
            Window.Current.SetTitleBar(TitleBarHandle);

            ChatsList.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            var header = ElementCompositionPreview.GetElementVisual(PageHeader);
            header.Clip = header.Compositor.CreateInsetClip();

            //var show = !((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats;
            var show = !((TLViewModelBase)ViewModel).Settings.HideArchivedChats;

            ArchivedChatsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Dispose()
        {
            try
            {
                Bindings.StopTracking();

                var viewModel = ViewModel;
                if (viewModel != null)
                {
                    viewModel.PlaybackService.PropertyChanged -= OnCurrentItemChanged;

                    viewModel.Settings.Delegate = null;
                    viewModel.Chats.Delegate = null;

                    viewModel.Aggregator.Unsubscribe(this);
                    viewModel.Dispose();
                }

                MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated -= OnNavigated;

                MasterDetail.Dispose();
                SettingsView?.Dispose();
            }
            catch { }
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 0), GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[3].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 6), GridUnitType.Pixel);

            StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 0), GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[3].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 6), GridUnitType.Pixel);

            StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        private void InitializeLocalization()
        {
            TabChats.Header = Strings.Resources.FilterChats;
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
        }

        public void Handle(UpdateFileDownloads update)
        {
            this.BeginOnUIThread(() => Downloads.UpdateFileDownloads(update));
        }

        public void Handle(UpdateChatPosition update)
        {
            if (update.Position.List is ChatListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ProtoService, new ChatListArchive()));
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
            if (update.User.Id == _protoService.Options.MyId)
            {
                this.BeginOnUIThread(() =>
                {
                    LogoBasic.Visibility = _protoService.IsPremium ? Visibility.Collapsed : Visibility.Visible;
                    LogoPremium.Visibility = _protoService.IsPremium ? Visibility.Visible : Visibility.Collapsed;

                    Photo.SetUser(_protoService, update.User, 28);
                    PhotoSide?.SetUser(_protoService, update.User, 28);
                });
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (update.UserId != _cacheService.Options.MyId && update.UserId != 777000 && _protoService.TryGetChatFromUser(update.UserId, out long chatId))
            {
                Handle(chatId, (chatView, chat) => chatView.UpdateUserStatus(chat, update.Status));
            }
        }

        public void Handle(UpdateChatAction update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatActions(chat, ViewModel.ProtoService.GetChatActions(chat.Id)));
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
            var response = await _protoService.SendAsync(new CreateSecretChat(update.SecretChat.Id));
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
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ProtoService, update.ChatList));
            }
        }

        private void Handle(long chatId, long messageId, Action<Chat> update, Action<ChatCell, Chat> action)
        {
            var chat = _cacheService.GetChat(chatId);
            if (chat.LastMessage == null || chat.LastMessage.Id != messageId)
            {
                return;
            }

            update(chat);

            //var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            //if (position == null)
            //{
            //    return;
            //}

            this.BeginOnUIThread(() =>
            {
                var container = ChatsList.ContainerFromItem(chat) as ListViewItem;
                if (container == null)
                {
                    return;
                }

                var chatView = container.ContentTemplateRoot as ChatCell;
                if (chatView != null)
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(long chatId, Action<ChatCell, Chat> action)
        {
            var chat = _cacheService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            //var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            //if (position == null)
            //{
            //    return;
            //}

            Handle(chat, action);
        }

        private void Handle(Chat chat, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                var container = ChatsList.ContainerFromItem(chat) as ListViewItem;
                if (container == null)
                {
                    return;
                }

                var chatView = container.ContentTemplateRoot as ChatCell;
                if (chatView != null)
                {
                    action(chatView, chat);
                }
            });
        }

        public void Handle(UpdateChatFilters update)
        {
            this.BeginOnUIThread(() =>
            {
                ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && update.ChatFilters.Count > 0);
                ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && update.ChatFilters.Count > 0);
                ShowHideArchive(ViewModel.SelectedFilter?.ChatList is ChatListMain or null);

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

        public void Handle(UpdateConnectionState update)
        {
            this.BeginOnUIThread(() =>
            {
                SetProxyVisibility(_cacheService.Options.ExpectBlocking, _cacheService.Options.EnabledProxyId, update.State);

                switch (update.State)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                        ShowState(Strings.Resources.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting connecting:
                        ShowState(Strings.Resources.Connecting);
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        ShowState(Strings.Resources.ConnectingToProxy);
                        break;
                    case ConnectionStateUpdating updating:
                        ShowState(Strings.Resources.Updating);
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
                this.BeginOnUIThread(() => SetProxyVisibility(_cacheService.Options.ExpectBlocking, _cacheService.Options.EnabledProxyId, _cacheService.GetConnectionState()));
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

            var peer = FrameworkElementAutomationPeer.FromElement(StateLabel);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }

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
            StateLabel.Text = Strings.Resources.AppName;
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

                    GroupCallBanner.Update(ViewModel.GroupCallService);
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

        public void Handle(UpdateChatFiltersLayout update)
        {
            this.BeginOnUIThread(() =>
            {
                ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0);
                ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0);
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
            if ((show && ChatTabs?.Visibility == Visibility.Visible) || (!show && (ChatTabs == null || ChatTabs.Visibility == Visibility.Collapsed || _tabsTopCollapsed)))
            {
                return;
            }

            if (show)
            {
                _tabsTopCollapsed = false;
            }

            if (ChatTabs == null)
            {
                FindName(nameof(ChatTabs));
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                // Too early, no animation needed
                DialogsPanel.Margin = new Thickness();

                if (show)
                {
                    _tabsTopCollapsed = false;
                }
                else
                {
                    ChatTabs.Visibility = Visibility.Collapsed;
                }

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

                DialogsPanel.Margin = new Thickness();

                if (show)
                {
                    _tabsTopCollapsed = false;
                }
                else
                {
                    ChatTabs.Visibility = Visibility.Collapsed;
                }
            };

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -32, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            //offset.Duration = TimeSpan.FromMilliseconds(150);

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
            if ((show && ChatTabsLeft?.Visibility == Visibility.Visible) || (!show && (ChatTabsLeft == null || ChatTabsLeft.Visibility == Visibility.Collapsed || _tabsLeftCollapsed)))
            {
                return;
            }

            _tabsLeftCollapsed = !show;
            Root?.SetSidebarEnabled(show);

            Photo.Visibility = show || rpMasterTitlebar.SelectedIndex == 3
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (ChatTabsLeft == null)
            {
                FindName(nameof(ChatTabsLeft));
            }

            if (show && PhotoSide != null)
            {
                if (_protoService.TryGetUser(_protoService.Options.MyId, out User user))
                {
                    PhotoSide.SetUser(_protoService, user, 28);
                }
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                // Too early, no animation needed
                ChatsList.Margin = new Thickness();

                if (show)
                {
                    _tabsLeftCollapsed = false;
                }
                else
                {
                    ChatTabs.Visibility = Visibility.Collapsed;
                }

                return;
            }

            ChatTabsLeft.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, 0, 0, -40);

            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var header = ElementCompositionPreview.GetElementVisual(ChatTabsView);

            parent.Clip = null;

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ChatsList.Margin = new Thickness();

                if (show)
                {
                    _tabsLeftCollapsed = false;
                }
                else
                {
                    ChatTabsLeft.Visibility = Visibility.Collapsed;
                }
            };

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            //offset.Duration = TimeSpan.FromMilliseconds(150);

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
            if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
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
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFilter));
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
                    || ViewModel.Filters.Count > 0 && !ViewModel.Chats.Items.ChatList.ListEquals(ViewModel.Filters[0].ChatList))
                {
                    UpdateFilter(ViewModel.Filters.Count > 0 ? ViewModel.Filters[0] : ChatFilterViewModel.Main);
                    args.Handled = true;
                }
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_protoService.TryGetUser(_protoService.Options.MyId, out User user))
            {
                LogoBasic.Visibility = _protoService.IsPremium ? Visibility.Collapsed : Visibility.Visible;
                LogoPremium.Visibility = _protoService.IsPremium ? Visibility.Visible : Visibility.Collapsed;

                Photo.SetUser(_protoService, user, 28);
                PhotoSide?.SetUser(_protoService, user, 28);
            }

            Subscribe();
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
            WindowContext.Current.AcceleratorKeyActivated += OnAcceleratorKeyActivated;

            OnStateChanged(null, null);

            ShowHideBanner(ViewModel.PlaybackService.CurrentItem != null);

            var update = new UpdateConnectionState(ViewModel.CacheService.GetConnectionState());
            if (update.State != null)
            {
                Handle(update);
                ViewModel.Aggregator.Publish(update);
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

                var confirm = await MessagePopup.ShowAsync("Unigram has previously failed to launch because the device storage was full.\r\n\r\nMake sure there's enough storage space available and press **OK** to continue.", "Disk storage is full", Strings.Resources.OK, Strings.Resources.StorageUsage);
                if (confirm == ContentDialogResult.Secondary)
                {
                    MasterDetail.NavigationService.Navigate(typeof(SettingsStoragePage));
                }
            }
        }

        private void Subscribe()
        {
            ViewModel.Aggregator.Subscribe<UpdateFileDownloads>(this, Handle)
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
                //.Subscribe<UpdateMessageContent>(Handle)
                .Subscribe<UpdateSecretChat>(Handle)
                .Subscribe<UpdateChatFilters>(Handle)
                .Subscribe<UpdateChatNotificationSettings>(Handle)
                .Subscribe<UpdatePasscodeLock>(Handle)
                .Subscribe<UpdateConnectionState>(Handle)
                .Subscribe<UpdateOption>(Handle)
                .Subscribe<UpdateCallDialog>(Handle)
                .Subscribe<UpdateChatFiltersLayout>(Handle)
                .Subscribe<UpdateConfetti>(Handle);
        }

        private void OnCurrentItemChanged(object sender, PropertyChangedEventArgs e)
        {
            this.BeginOnUIThread(() => ShowHideBanner(ViewModel.PlaybackService.CurrentItem != null));
        }

        private bool _bannerCollapsed;

        private async void ShowHideBanner(bool show)
        {
            if (show && Playback == null)
            {
                FindName(nameof(Playback));
                Playback.Update(ViewModel.ProtoService, ViewModel.PlaybackService, ViewModel.NavigationService);
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
            WindowContext.Current.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;

            var titleBar = CoreApplication.GetCurrentView().TitleBar;
            titleBar.IsVisibleChanged -= CoreTitleBar_LayoutMetricsChanged;
            titleBar.LayoutMetricsChanged -= CoreTitleBar_LayoutMetricsChanged;

            Bindings.StopTracking();

            _unloaded = true;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (MasterDetail.NavigationService.Frame.Content is BlankPage == false)
            {
                return;
            }

            var character = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0 || char.IsControl(character[0]) || char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused == null || (focused is TextBox == false && focused is RichEditBox == false))
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

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            var invoked = ViewModel.ShortcutService.Process(args);
            foreach (var command in invoked.Commands)
            {
#if DEBUG
                if (command == ShortcutCommand.Quit)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    return;
                }
#endif

                ProcessChatCommands(command, args);
                ProcessFolderCommands(command, args);
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, AcceleratorKeyEventArgs args)
        {
            if (command is ShortcutCommand.Search)
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
            else if (command is ShortcutCommand.Quit or ShortcutCommand.Close)
            {
                if (command is ShortcutCommand.Quit && App.Connection != null)
                {
                    await App.Connection.SendMessageAsync(new Windows.Foundation.Collections.ValueSet { { "Exit", string.Empty } });
                }

                ApplicationView.GetForCurrentView().Consolidated += (s, args) =>
                {
                    Application.Current.Exit();
                };

                if (await ApplicationView.GetForCurrentView().TryConsolidateAsync())
                {
                    return;
                }

                Application.Current.Exit();
            }
            else if (command == ShortcutCommand.Lock)
            {
                Lock_Click(null, null);
            }
        }

        private void ProcessFolderCommands(ShortcutCommand command, AcceleratorKeyEventArgs args)
        {
            var folders = ViewModel.Filters;
            if (folders.IsEmpty())
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
                    UpdateFilter(folders[index], false);
                }
            }
        }

        private async void ProcessChatCommands(ShortcutCommand command, AcceleratorKeyEventArgs args)
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

                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(ViewModel.CacheService.Options.MyId, false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    MasterDetail.NavigationService.GoBackAt(0, false);
                }
            }
            else if (command is >= ShortcutCommand.ChatPinned1 and <= ShortcutCommand.ChatPinned5)
            {
                var folders = ViewModel.Filters;
                if (folders.Count > 0)
                {
                    return;
                }

                var index = command - ShortcutCommand.ChatPinned1;

                var response = await ViewModel.ProtoService.GetChatListAsync(new ChatListMain(), 0, (int)ViewModel.CacheService.Options.PinnedChatCountMax * 2 + 1);
                if (response is Telegram.Td.Api.Chats chats && index >= 0 && index < chats.ChatIds.Count)
                {
                    for (int i = 0; i < chats.ChatIds.Count; i++)
                    {
                        var chat = ViewModel.CacheService.GetChat(chats.ChatIds[i]);
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
                var already = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == ViewModel.Chats.SelectedItem);
                if (already == null)
                {
                    return;
                }

                index = ViewModel.Chats.Items.IndexOf(already) + offset;
            }

            if (index >= 0 && index < ViewModel.Chats.Items.Count)
            {
                ChatsList.SelectedIndex = index;

                if (navigate)
                {
                    Navigate(ChatsList.SelectedItem);
                }
            }
            else if (index < 0 && offset == -1 && !navigate)
            {
                Search_Click(null, null);
            }
        }

        public void ScrollFolder(int offset, bool navigate)
        {
            var already = ViewModel.SelectedFilter;
            if (already == null)
            {
                return;
            }

            var index = ViewModel.Filters.IndexOf(already);
            if (offset == int.MaxValue)
            {
                index = ViewModel.Filters.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index += offset;
            }

            if (index >= 0 && index < ViewModel.Filters.Count)
            {
                UpdateFilter(ViewModel.Filters[index], false);
            }
        }

        public void Initialize()
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main", Frame, ViewModel.ProtoService.SessionId);
                MasterDetail.NavigationService.FrameFacade.Navigating += OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
            ViewModel.Chats.NavigationService = MasterDetail.NavigationService;
            ViewModel.Contacts.NavigationService = MasterDetail.NavigationService;
            ViewModel.Calls.NavigationService = MasterDetail.NavigationService;
            ViewModel.Settings.NavigationService = MasterDetail.NavigationService;

            ArchivedChats.UpdateChatList(ViewModel.ProtoService, new ChatListArchive());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Initialize();
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
                    var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
                else if (data.ContainsKey("chat_id") && int.TryParse(data["chat_id"], out int chat_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreateBasicGroupChat(chat_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
                else if (data.ContainsKey("channel_id") && int.TryParse(data["channel_id"], out int channel_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreateSupergroupChat(channel_id, false));
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
                    if (popup.Child is GalleryView gallery)
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
                await SharePopup.GetForCurrentView().ShowAsync(package);
            }

            if (MessageHelper.IsTelegramUrl(scheme))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ProtoService, MasterDetail.NavigationService, scheme);
            }
            else if (scheme.Scheme.Equals("ms-contact-profile") || scheme.Scheme.Equals("ms-ipmessaging"))
            {
                var query = scheme.Query.ParseQueryString();
                if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
            }
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            var frame = sender as Frame;
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

            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                allowed &= e.SourcePageType != typeof(BlankPage);
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(allowed, true);
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
            UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
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
                StateLabel.Visibility = Visibility.Visible;
            }
            else
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ChatsList.SelectionMode = ListViewSelectionMode.Single;
                    ChatsList.SelectedItem = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == ViewModel.Chats.SelectedItem);
                }

                Header.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
                StateLabel.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
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

            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                allowed &= frame.CurrentSourcePageType != typeof(BlankPage);
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(allowed, false);
            }
        }

        private void UpdatePaneToggleButtonVisibility()
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                if (MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage))
                {
                    if (rpMasterTitlebar.SelectedIndex != 0 || ViewModel.Chats.Items.ChatList is ChatListArchive || !_searchCollapsed || ChatsList.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        SetPaneToggleButtonVisibility(PaneToggleButtonVisibility.Back);
                    }
                    else
                    {
                        SetPaneToggleButtonVisibility(PaneToggleButtonVisibility.Visible);
                    }
                }
                else
                {
                    SetPaneToggleButtonVisibility(PaneToggleButtonVisibility.Collapsed);
                }
            }
            else if (rpMasterTitlebar.SelectedIndex != 0 || ViewModel.Chats.Items.ChatList is ChatListArchive || !_searchCollapsed || ChatsList.SelectionMode == ListViewSelectionMode.Multiple)
            {
                SetPaneToggleButtonVisibility(PaneToggleButtonVisibility.Back);
            }
            else
            {
                SetPaneToggleButtonVisibility(PaneToggleButtonVisibility.Visible);
            }
        }

        private bool _visibility = false;

        private void SetPaneToggleButtonVisibility(PaneToggleButtonVisibility visibility)
        {
            if (MasterDetail.NavigationService.CanGoBack)
            {
                visibility = PaneToggleButtonVisibility.Back;
            }

            Root?.SetPaneToggleButtonVisibility(visibility);

            var visible = visibility == PaneToggleButtonVisibility.Back;
            if (visible != _visibility)
            {
                var logo = ElementCompositionPreview.GetElementVisual(TitleBarLogo);
                var label = ElementCompositionPreview.GetElementVisual(StateLabel);

                ElementCompositionPreview.SetIsTranslationEnabled(TitleBarLogo, true);
                ElementCompositionPreview.SetIsTranslationEnabled(StateLabel, true);

                var anim = logo.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(visibility == PaneToggleButtonVisibility.Back ? 0 : 1, new Vector3(0, 0, 0));
                anim.InsertKeyFrame(visibility == PaneToggleButtonVisibility.Back ? 1 : 0, new Vector3(36, 0, 0));

                logo.StartAnimation("Translation", anim);
                label.StartAnimation("Translation", anim);

                _visibility = visible;
            }
        }

        private void UpdateListViewsSelectedItem(long chatId)
        {
            ViewModel.Chats.SelectedItem = chatId;

            var dialog = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == chatId);
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                ChatsList.SelectedItem = dialog;
            }
        }

        public PaneToggleButtonVisibility EvaluatePaneToggleButtonVisibility()
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                return MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage)
                    ? PaneToggleButtonVisibility.Visible
                    : PaneToggleButtonVisibility.Collapsed;
            }
            else
            {
                return PaneToggleButtonVisibility.Visible;
            }
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

                if (ViewModel.Chats.SelectedItems.IsEmpty())
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

                MasterDetail.NavigationService.NavigateToChat(message.ChatId, message: message.Id, force: false);
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
                    ViewModel.ProtoService.Send(new AddRecentlyFoundChat(result.Chat.Id));
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
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(user.Id, false));
                if (response is Chat)
                {
                    item = response as Chat;
                }
            }

            if (item is Chat chat)
            {
                ViewModel.Chats.SelectedItem = chat.Id;

                MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                MasterDetail.NavigationService.GoBackAt(0, false);
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
                    var items = ViewModel.Chats.Search;
                    if (items != null && string.Equals(SearchField.Text, items.Query))
                    {
                        await items.LoadMoreItemsAsync(2);
                        await items.LoadMoreItemsAsync(3);
                        await items.LoadMoreItemsAsync(4);
                    }
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

        private void TopChat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            flyout.CreateFlyoutItem(_ => true, ViewModel.Chats.TopChatDeleteCommand, chat, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        private void Call_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var call = element.Tag as TLCallGroup;

            flyout.CreateFlyoutItem(_ => true, ViewModel.Calls.CallDeleteCommand, call, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }




        private Visibility CallDelete_Loaded(TLCallGroup group)
        {
            return Visibility.Visible;
        }

        #endregion

        #region Binding

        private string ConvertGeoLive(int count, IList<Message> items)
        {
            //if (count > 1)
            //{
            //    return string.Format("sharing to {0} chats", count);
            //}
            //else if (count == 1 && items[0].Parent is ITLDialogWith with)
            //{
            //    return string.Format("sharing to {0}", with.DisplayName);
            //}

            return null;
        }

        private string ConvertSortedBy(bool epoch)
        {
            return epoch ? Strings.Resources.SortedByLastSeen : Strings.Resources.SortedByName;
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
                    FindName(nameof(ContactsRoot));
                    break;
                case 2:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Calls);
                    FindName(nameof(CallsRoot));
                    break;
                case 3:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Settings);

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

            MasterDetail.NavigationService.GoBackAt(0);

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

            Photo.Visibility = _tabsLeftCollapsed && rpMasterTitlebar.SelectedIndex != 3
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateHeader()
        {
            ChatsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ContactsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

            SearchField.PlaceholderText = rpMasterTitlebar.SelectedIndex == 3 ? Strings.Resources.SearchInSettings : Strings.Resources.Search;
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

            var chats = ElementCompositionPreview.GetElementVisual(DialogsPanel);
            var panel = ElementCompositionPreview.GetElementVisual(DialogsSearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3(DialogsPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (show)
                {
                    _searchCollapsed = false;
                    DialogsPanel.Visibility = Visibility.Collapsed;
                }
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

            ComposeButton.Visibility = string.IsNullOrEmpty(SearchField.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (rpMasterTitlebar.SelectedIndex == 0)
            {
                //DialogsPanel.Visibility = Visibility.Collapsed;
                ShowHideSearch(true);

                if (string.Equals(ViewModel.Chats.Search?.Query, SearchField.Text))
                {
                    return;
                }

                if (ViewModel.Chats.SearchFilters.IsEmpty() && string.IsNullOrEmpty(SearchField.Text))
                {
                    var top = ViewModel.Chats.TopChats = new TopChatsCollection(ViewModel.ProtoService, new TopChatCategoryUsers(), 30);
                    await top.LoadMoreItemsAsync(0);
                }
                else
                {
                    ViewModel.Chats.TopChats = null;
                }

                var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters);
                await items.LoadMoreItemsAsync(0);
                await items.LoadMoreItemsAsync(1);
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

                    var items = ViewModel.Contacts.Search = new SearchUsersCollection(ViewModel.ProtoService, SearchField.Text);
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

            FocusTarget.Focus(FocusState.Programmatic);
            SearchField.Text = string.Empty;
            ComposeButton.Visibility = Visibility.Visible;

            if (ContactsPanel != null)
            {
                ContactsPanel.Visibility = Visibility.Visible;
            }

            if (SettingsView != null)
            {
                SettingsView.Visibility = Visibility.Visible;
            }

            ViewModel.Chats.SearchFilters.Clear();
            ViewModel.Chats.TopChats = null;
            ViewModel.Chats.Search = null;
            ViewModel.Contacts.Search = null;
            ViewModel.Settings.Results.Clear();
        }

        private async void Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (rpMasterTitlebar.SelectedIndex == 0 && e.Key == Windows.System.VirtualKey.Back)
            {
                if (SearchField.SelectionStart == 0 && SearchField.SelectionLength == 0)
                {
                    if (ViewModel.Chats.SearchFilters?.Count > 0)
                    {
                        e.Handled = true;
                        ViewModel.Chats.SearchFilters.RemoveAt(ViewModel.Chats.SearchFilters.Count - 1);

                        var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters);
                        await items.LoadMoreItemsAsync(0);
                        await items.LoadMoreItemsAsync(1);
                        await items.LoadMoreItemsAsync(2);
                        await items.LoadMoreItemsAsync(3);
                        await items.LoadMoreItemsAsync(4);

                        return;
                    }
                }
            }

            Grid activePanel;
            ListView activeList;
            CollectionViewSource activeResults;

            switch (rpMasterTitlebar.SelectedIndex)
            {
                case 0:
                    FindName(nameof(DialogsSearchPanel));
                    activePanel = DialogsPanel;
                    activeList = DialogsSearchListView;
                    activeResults = ChatsResults;
                    break;
                case 1:
                    FindName(nameof(ContactsSearchListView));
                    activePanel = ContactsPanel;
                    activeList = ContactsSearchListView;
                    activeResults = ContactsResults;
                    break;
                default:
                    return;
            }

            if (activePanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if (e.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down)
            {
                var index = e.Key == Windows.System.VirtualKey.Up ? -1 : 1;
                var next = activeList.SelectedIndex + index;
                if (next >= 0 && next < activeResults?.View.Count)
                {
                    activeList.SelectedIndex = next;
                    activeList.ScrollIntoView(activeList.SelectedItem);
                }

                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var index = Math.Max(activeList.SelectedIndex, 0);
                var container = activeList.ContainerFromIndex(index) as ListViewItem;
                if (container != null)
                {
                    var peer = new ListViewItemAutomationPeer(container);
                    var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }

                e.Handled = true;
            }
        }

        #endregion

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Passcode.IsEnabled)
            {
                return;
            }

            ViewModel.Passcode.Lock();
            App.ShowPasscode(false);
        }

        private void OnUpdate(object sender, EventArgs e)
        {
            //Bindings.Update();
        }

        private void DialogsSearchListView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = DialogsSearchListView.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.ItemContainer.ContentTemplate = DialogsSearchListView.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void DialogsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is SearchResult result)
            {
                var content = args.ItemContainer.ContentTemplateRoot as UserCell;
                if (content == null)
                {
                    return;
                }

                args.ItemContainer.Tag = result.Chat;
                content.UpdateSearchResult(_protoService, args, DialogsSearchListView_ContainerContentChanging);
            }
            else if (args.Item is Message message)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
                if (content == null)
                {
                    return;
                }

                args.ItemContainer.Tag = null;
                content.UpdateMessage(ViewModel.ProtoService, message);
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

            var content = args.ItemContainer.ContentTemplateRoot as UserCell;
            var user = args.Item as User;

            content.UpdateUser(ViewModel.ProtoService, user, args, UsersListView_ContainerContentChanging);
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
            if (icon != null)
            {
                icon.InvalidateMeasure();
            }

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

        private void ContactsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var result = args.Item as SearchResult;
            var chat = result.Chat;
            var user = result.User ?? ViewModel.ProtoService.GetUser(chat);

            if (user == null)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                if (result.IsPublic)
                {
                    subtitle.Text = $"@{user.Username}";
                }
                else
                {
                    subtitle.Text = LastSeenConverter.GetLabel(user, true);
                }

                if (subtitle.Text.StartsWith($"@{result.Query}", StringComparison.OrdinalIgnoreCase))
                {
                    var highligher = new TextHighlighter();
                    highligher.Foreground = new SolidColorBrush(Colors.Red);
                    highligher.Background = new SolidColorBrush(Colors.Transparent);
                    highligher.Ranges.Add(new TextRange { StartIndex = 1, Length = result.Query.Length });

                    subtitle.TextHighlighters.Add(highligher);
                }
                else
                {
                    subtitle.TextHighlighters.Clear();
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(ContactsSearchListView_ContainerContentChanging);
            }

            args.Handled = true;
        }

        private void TopChats_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var chat = args.Item as Chat;

            args.ItemContainer.Tag = chat;
            content.Tag = chat;

            var grid = content.Children[0] as Grid;

            var photo = grid.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            photo.SetChat(ViewModel.ProtoService, chat, 48);
            title.Text = ViewModel.ProtoService.GetTitle(chat, true);

            var badge = grid.Children[1] as InfoBadge;
            badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            badge.Value = chat.UnreadCount;

            var user = ViewModel.CacheService.GetUser(chat);
            if (user != null)
            {
                var online = grid.Children[2] as Border;
                online.Visibility = user.Status is UserStatusOnline ? Visibility.Visible : Visibility.Collapsed;
            }
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

            content.UpdateCall(ViewModel.ProtoService, call);
        }

        private void SetPivotIndex(int index)
        {
            if (rpMasterTitlebar.SelectedIndex != index)
            {
                rpMasterTitlebar.IsLocked = false;
                rpMasterTitlebar.SelectedIndex = index;
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
            else if (destination == RootDestination.SavedMessages)
            {
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(ViewModel.CacheService.Options.MyId, false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                }
            }
            else if (destination == RootDestination.Tips && Uri.TryCreate(Strings.Resources.TelegramFeaturesUrl, UriKind.Absolute, out Uri tipsUri))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ProtoService, MasterDetail.NavigationService, tipsUri);
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ProtoService, MasterDetail.NavigationService, "unigram", null, null);
            }
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            var scrollViewer = ChatsList.GetScrollViewer();
            if (scrollViewer != null)
            {
                scrollViewer.ChangeView(null, 0, null);
            }
        }

        private void Proxy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsProxiesPage));
        }

        private void ChatBackgroundPresenter_Loading(FrameworkElement sender, object args)
        {
            if (sender is ChatBackgroundPresenter presenter)
            {
                presenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);
            }
        }

        private void ChatsNearby_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(ChatsNearbyPage));
        }

        private ChatFilterViewModel ConvertFilter(ChatFilterViewModel filter)
        {
            ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0 && filter.ChatList is not ChatListArchive);
            ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0);
            ShowHideArchive(filter?.ChatList is ChatListMain or null && ViewModel.Chats.Items.ChatList is not ChatListArchive);

            UpdatePaneToggleButtonVisibility();

            return filter;
        }

        private void ConvertFilterBack(object obj)
        {
            if (obj is ChatFilterViewModel filter && !filter.IsNavigationItem && ViewModel.Chats.Items.ChatList is not ChatListArchive)
            {
                UpdateFilter(filter);
            }
        }

        public void ArchivedChats_Click(object sender, RoutedEventArgs e)
        {
            UpdateFilter(ChatFilterViewModel.Archive);
        }

        private void ChatFilter_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var filter = ChatFilters?.ItemFromContainer(sender) as ChatFilterViewModel;

            if (filter == null)
            {
                filter = ChatFiltersSide.ItemFromContainer(sender) as ChatFilterViewModel;
            }

            if (filter.IsNavigationItem)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (filter.ChatFilterId == Constants.ChatListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.FilterEditCommand, filter, Strings.Resources.FilterEditAll, new FontIcon { Glyph = Icons.Edit });
                flyout.CreateFlyoutItem(ViewModel.FilterMarkAsReadCommand, filter, Strings.Resources.MarkAsRead, new FontIcon { Glyph = Icons.MarkAsRead });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.FilterEditCommand, filter, Strings.Resources.FilterEdit, new FontIcon { Glyph = Icons.Edit });
                flyout.CreateFlyoutItem(ViewModel.FilterMarkAsReadCommand, filter, Strings.Resources.MarkAsRead, new FontIcon { Glyph = Icons.MarkAsRead });
                flyout.CreateFlyoutItem(ViewModel.FilterAddCommand, filter, Strings.Resources.FilterAddChats, new FontIcon { Glyph = Icons.Add });
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.FilterDeleteCommand, filter, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }

            flyout.ShowAt(element, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
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
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.Resources.AccDescrExpandPanel, new FontIcon { Glyph = Icons.Expand });
            //}
            //else
            //{
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.Resources.AccDescrCollapsePanel, new FontIcon { Glyph = Icons.Collapse });
            //}

            flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.Resources.lng_context_archive_to_menu, new FontIcon { Glyph = Icons.Collapse });
            flyout.CreateFlyoutItem(ViewModel.FilterMarkAsReadCommand, ChatFilterViewModel.Archive, Strings.Resources.MarkAllAsRead, new FontIcon { Glyph = Icons.MarkAsRead });

            args.ShowAt(flyout, element);
        }

        public async void ToggleArchive()
        {
            ViewModel.ToggleArchiveCommand.Execute();

            ArchivedChatsPanel.Visibility = Visibility.Visible;
            //ArchivedChatsCompactPanel.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            var show = !((TLViewModelBase)ViewModel).Settings.HideArchivedChats;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;

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

                ArchivedChatsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                ChatsList.Margin = new Thickness();

                Root.UpdateSessions();

                if (show is false)
                {
                    Window.Current.ShowTeachingTip(Photo, Strings.Resources.lng_context_archive_to_menu_info, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.BottomRight);
                }
            };

            var panelY = ArchivedChatsPanel.ActualSize.Y;
            var compactY = 0; //(float)ArchivedChatsCompactPanel.ActualHeight;

            ChatsList.Margin = new Thickness(0, 0, 0, -(panelY - compactY));

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

            _archiveCollapsed = show;
            ArchivedChatsPresenter.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;

            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var chats = ElementCompositionPreview.GetElementVisual(element);
            var panel = ElementCompositionPreview.GetElementVisual(ArchivedChatsPanel);

            parent.Clip = chats.Compositor.CreateInsetClip();

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                ChatsList.Margin = new Thickness();

                if (show)
                {
                    _archiveCollapsed = false;
                }
                else
                {
                    ArchivedChatsPresenter.Visibility = Visibility.Collapsed;
                }
            };

            var y = ArchivedChatsPresenter.ActualSize.Y;

            ChatsList.Margin = new Thickness(0, 0, 0, -y);

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

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, y0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, y1, 0));
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

        private void UpdateFilter(ChatFilterViewModel filter, bool update = true)
        {
            ViewModel.SelectedFilter = filter;

            if (filter.ChatList is ChatListArchive)
            {
                _shouldGoBackWithDetail = false;
            }

            if (update)
            {
                ConvertFilter(filter);
            }

            Search_LostFocus(null, null);
            return;

            var visual = ElementCompositionPreview.GetElementVisual(TabChats);
            ElementCompositionPreview.SetIsTranslationEnabled(TabChats, true);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ViewModel.SelectedFilter = filter;

                if (update)
                {
                    ConvertFilter(filter);
                }

                var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, new Vector3(0, 16, 0));
                offset.InsertKeyFrame(1, new Vector3(0, 0, 0));
                offset.Duration = TimeSpan.FromMilliseconds(150);

                var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = TimeSpan.FromMilliseconds(150);

                visual.StartAnimation("Translation", offset);
                visual.StartAnimation("Opacity", opacity);
            };

            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0, new Vector3());
            offset.InsertKeyFrame(1, new Vector3(0, -16, 0));
            offset.Duration = TimeSpan.FromMilliseconds(150);

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);
            opacity.Duration = TimeSpan.FromMilliseconds(150);

            visual.StartAnimation("Translation", offset);
            visual.StartAnimation("Opacity", opacity);

            batch.End();
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
            MainHeader.Visibility = Visibility.Visible;

            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            var info = ElementCompositionPreview.GetElementVisual(MainHeader);

            manage.Offset = new Vector3(show ? -32 : 0, 0, 0);
            manage.Opacity = show ? 0 : 1;

            info.Offset = new Vector3(show ? 0 : 32, 0, 0);
            info.Opacity = show ? 1 : 0;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                manage.Offset = new Vector3(show ? 0 : -32, 0, 0);
                manage.Opacity = show ? 1 : 0;

                info.Offset = new Vector3(show ? 32 : 0, 0, 0);
                info.Opacity = show ? 0 : 1;

                if (show)
                {
                    MainHeader.Visibility = Visibility.Collapsed;
                    ManagePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    MainHeader.Visibility = Visibility.Visible;
                    ManagePanel.Visibility = Visibility.Collapsed;

                    ViewModel.Chats.SelectedItems.Clear();
                }
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(-32, 0, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3());

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            var offset2 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, new Vector3());
            offset2.InsertKeyFrame(show ? 1 : 0, new Vector3(32, 0, 0));

            var opacity2 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);

            manage.StartAnimation("Offset", offset1);
            manage.StartAnimation("Opacity", opacity1);
            info.StartAnimation("Offset", offset2);
            info.StartAnimation("Opacity", opacity2);

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
                var muted = ViewModel.Chats.SelectedItems.Any(x => ViewModel.CacheService.Notifications.GetMutedFor(x) > 0);
                ManageMute.Glyph = muted ? Icons.Alert : Icons.AlertOff;
                Automation.SetToolTip(ManageMute, muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications);

                var unread = ViewModel.Chats.SelectedItems.Any(x => x.IsUnread());
                ManageMark.Glyph = unread ? Icons.MarkAsRead : Icons.MarkAsUnread;
                Automation.SetToolTip(ManageMark, unread ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread);

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

        public static string GetFilterIcon(ChatListFilterFlags filter)
        {
            if (filter == ChatListFilterFlags.ExcludeMuted)
            {
                return Icons.Alert;
            }
            else if (filter == ChatListFilterFlags.ExcludeRead)
            {
                return Icons.MarkAsUnread; //FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
            }
            else if (filter == ChatListFilterFlags.ExcludeArchived)
            {
                return Icons.Archive;
            }
            else if (filter == ChatListFilterFlags.IncludeContacts)
            {
                return Icons.Person;
            }
            else if (filter == ChatListFilterFlags.IncludeNonContacts)
            {
                return Icons.PersonQuestionMark;
            }
            else if (filter == ChatListFilterFlags.IncludeGroups)
            {
                return Icons.People;
            }
            else if (filter == ChatListFilterFlags.IncludeChannels)
            {
                return Icons.Megaphone;
            }
            else if (filter == ChatListFilterFlags.IncludeBots)
            {
                return Icons.Bot;
            }

            return null;
        }

        private void ChatFilters_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var list = sender as TopNavView;
            if (list == null)
            {
                return;
            }

            if (e.Items.Count > 1 || (e.Items[0] is ChatFilterViewModel filter && filter.ChatList is ChatListMain && !_protoService.IsPremium))
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var minimum = _protoService.IsPremium ? 2 : 3;

                var items = ViewModel?.Filters;
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

        private void ChatFilters_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ChatFilterViewModel filter)
            {
                var items = ViewModel?.Filters;
                var index = items.IndexOf(filter);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare.ChatList is ChatListMain && index > 0 && !_protoService.IsPremium)
                {
                    compare = items[index + 1];
                }

                if (compare.ChatList is ChatListMain && !_protoService.IsPremium)
                {
                    ViewModel.Handle(new UpdateChatFilters(ViewModel.CacheService.ChatFilters, 0));
                }
                else
                {
                    var filters = items.Where(x => x.ChatList is ChatListFilter).Select(x => x.ChatFilterId).ToArray();
                    var main = _protoService.IsPremium ? items.IndexOf(items.FirstOrDefault(x => x.ChatList is ChatListMain)) : 0;

                    ViewModel.ProtoService.Send(new ReorderChatFilters(filters, main));
                }
            }
        }

        private void ChatFilter_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatFilterViewModel chatFilter)
            {
                var index = int.MaxValue - chatFilter.ChatFilterId;
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
                if (scrollingHost != null)
                {
                    scrollingHost.ChangeView(null, 0, null);
                }
            }
        }

        private void SearchFilters_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is ISearchChatsFilter filter)
            {
                var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
                if (content == null)
                {
                    return;
                }

                var glyph = content.Children[0] as TextBlock;
                glyph.Text = filter.Glyph ?? string.Empty;

                var title = content.Children[1] as TextBlock;
                title.Text = filter.Text ?? string.Empty;
            }
        }

        private async void SearchFilters_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ISearchChatsFilter filter)
            {
                ViewModel.Chats.SearchFilters.Add(filter);
                SearchField.Text = string.Empty;

                var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters);
                await items.LoadMoreItemsAsync(0);
                await items.LoadMoreItemsAsync(1);
                await items.LoadMoreItemsAsync(2);
                await items.LoadMoreItemsAsync(3);
                await items.LoadMoreItemsAsync(4);
            }
        }

        private void ArchivedChats_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ArchivedChats.UpdateChatList(ViewModel.ProtoService, new ChatListArchive());
        }

        private async void Downloads_Click(object sender, RoutedEventArgs e)
        {
            await new DownloadsPopup(ViewModel.SessionId, ViewModel.NavigationService).ShowQueuedAsync();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            Root.IsPaneOpen = true;
        }

        private void ChatFilters_ChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
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
                //args.ItemContainer.Style = ChatsList.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        public void PopupOpened()
        {
            Window.Current.SetTitleBar(null);
        }

        public void PopupClosed()
        {
            Window.Current.SetTitleBar(TitleBarHandle);
        }

        #region Context menu

        private async void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel.Chats;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            var position = chat?.GetPosition(viewModel.Items.ChatList);
            if (position == null)
            {
                return;
            }

            var muted = ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0;
            flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ChatArchiveCommand, chat, chat.Positions.Any(x => x.List is ChatListArchive) ? Strings.Resources.Unarchive : Strings.Resources.Archive, new FontIcon { Glyph = Icons.Archive });
            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.ChatPinCommand, chat, position.IsPinned ? Strings.Resources.UnpinFromTop : Strings.Resources.PinToTop, new FontIcon { Glyph = position.IsPinned ? Icons.PinOff : Icons.Pin });

            if (viewModel.Items.ChatList is ChatListFilter chatListFilter)
            {
                flyout.CreateFlyoutItem(viewModel.FolderRemoveCommand, (chatListFilter.ChatFilterId, chat), Strings.Resources.FilterRemoveFrom, new FontIcon { Glyph = Icons.FolderMove });
            }
            else
            {
                var response = await ViewModel.ProtoService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
                if (response != null && response.ChatListsValue.Count > 0)
                {
                    var filters = ViewModel.CacheService.ChatFilters;

                    var item = new MenuFlyoutSubItem();
                    item.Text = Strings.Resources.FilterAddTo;
                    item.Icon = new FontIcon { Glyph = Icons.FolderAdd, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                    foreach (var chatList in response.ChatListsValue.OfType<ChatListFilter>())
                    {
                        var filter = filters.FirstOrDefault(x => x.Id == chatList.ChatFilterId);
                        if (filter != null)
                        {
                            var icon = Icons.ParseFilter(filter.IconName);
                            var glyph = Icons.FilterToGlyph(icon);

                            item.CreateFlyoutItem(viewModel.FolderAddCommand, (filter.Id, chat), filter.Title, new FontIcon { Glyph = glyph.Item1 });
                        }
                    }

                    if (filters.Count < 10 && item.Items.Count > 0)
                    {
                        item.CreateFlyoutSeparator();
                        item.CreateFlyoutItem(viewModel.FolderCreateCommand, chat, Strings.Resources.CreateNewFilter, new FontIcon { Glyph = Icons.Add });
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
                mute.Text = Strings.Resources.Mute;
                mute.Icon = new FontIcon { Glyph = muted ? Icons.Alert : Icons.AlertOff, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                if (muted is false)
                {
                    mute.CreateFlyoutItem(true, () => { },
                        silent ? Strings.Resources.SoundOn : Strings.Resources.SoundOff,
                        new FontIcon { Glyph = silent ? Icons.MusicNote2 : Icons.MusicNoteOff2 });
                }

                mute.CreateFlyoutItem(ViewModel.Chats.ChatMuteForCommand, Tuple.Create<Chat, int?>(chat, 60 * 60), Strings.Resources.MuteFor1h, new FontIcon { Glyph = Icons.ClockAlarmHour });
                mute.CreateFlyoutItem(ViewModel.Chats.ChatMuteForCommand, Tuple.Create<Chat, int?>(chat, null), Strings.Resources.MuteForPopup, new FontIcon { Glyph = Icons.AlertSnooze });

                var toggle = mute.CreateFlyoutItem(
                    ViewModel.Chats.ChatNotifyCommand,
                    chat,
                    muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications,
                    new FontIcon { Glyph = muted ? Icons.Speaker : Icons.SpeakerOff });

                if (muted is false)
                {
                    toggle.Foreground = App.Current.Resources["DangerButtonBackground"] as Brush;
                }

                flyout.Items.Add(mute);

            }

            flyout.CreateFlyoutItem(DialogMark_Loaded, viewModel.ChatMarkCommand, chat, chat.IsUnread() ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread, new FontIcon { Glyph = chat.IsUnread() ? Icons.MarkAsRead : Icons.MarkAsUnread, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
            flyout.CreateFlyoutItem(DialogClear_Loaded, viewModel.ChatClearCommand, chat, Strings.Resources.ClearHistory, new FontIcon { Glyph = Icons.Broom });
            flyout.CreateFlyoutItem(DialogDelete_Loaded, viewModel.ChatDeleteCommand, chat, DialogDelete_Text(chat), new FontIcon { Glyph = Icons.Delete });

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.ChatOpenCommand, chat, "Open in new window", new FontIcon { Glyph = Icons.WindowNew });
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.ChatSelectCommand, chat, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
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
            //    var max = ViewModel.CacheService.Config.PinnedDialogsCountMax;

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
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source != null || chat.Id == 777000)
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source is ChatSourcePublicServiceAnnouncement)
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
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return string.IsNullOrEmpty(supergroup.Username) && !super.IsChannel;
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
                return Strings.Resources.PsaHide;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.Resources.LeaveChannelMenu : Strings.Resources.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.Resources.DeleteAndExit;
            }

            return Strings.Resources.Delete;
        }

        #endregion


    }

    public class HostedPage : Page
    {
        #region Header

        public UIElement Header
        {
            get => (UIElement)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(UIElement), typeof(HostedPage), new PropertyMetadata(null));

        #endregion

        #region Footer

        public UIElement Footer
        {
            get { return (UIElement)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(UIElement), typeof(HostedPage), new PropertyMetadata(null));

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

        #region IsNavigationRoot

        public bool IsNavigationRoot { get; set; } = false;

        #endregion
    }
}
