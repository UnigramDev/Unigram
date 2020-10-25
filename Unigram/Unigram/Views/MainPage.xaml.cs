using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Controls.Chats;
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
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class MainPage : Page,
        IRootContentPage,
        INavigatingPage,
        IChatsDelegate,
        IHandle<UpdateChatPosition>,
        IHandle<UpdateChatIsMarkedAsUnread>,
        IHandle<UpdateChatReadInbox>,
        IHandle<UpdateChatReadOutbox>,
        IHandle<UpdateChatUnreadMentionCount>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>,
        IHandle<UpdateUserChatAction>,
        IHandle<UpdateUserStatus>,
        IHandle<UpdateMessageMentionRead>,
        IHandle<UpdateUnreadChatCount>,
        //IHandle<UpdateMessageContent>,
        IHandle<UpdateSecretChat>,
        IHandle<UpdateChatFilters>,
        IHandle<UpdateChatNotificationSettings>,
        IHandle<UpdatePasscodeLock>,
        IHandle<UpdateFile>,
        IHandle<UpdateConnectionState>,
        IHandle<UpdateOption>,
        IHandle<UpdateCallDialog>,
        IHandle<UpdateChatListLayout>,
        IHandle<UpdateChatFiltersLayout>,
        IHandle<UpdateConfetti>
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        public RootPage Root { get; set; }

        private readonly ICacheService _cacheService;

        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MainViewModel>();

            _cacheService = ViewModel.CacheService;

            SettingsView.DataContext = ViewModel.Settings;
            ViewModel.Settings.Delegate = SettingsView;
            ViewModel.Chats.Delegate = this;
            ViewModel.Chats.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
            ViewModel.ArchivedChats.Delegate = this;
            ViewModel.ArchivedChats.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

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

            InputPane.GetForCurrentView().Showing += (s, args) => args.EnsuredFocusedElementInView = true;

            var updateShadow = DropShadowEx.Attach(UpdateShadow, 20, 0.25f);
            updateShadow.RelativeSizeAdjustment = Vector2.One;

            var folderShadow = DropShadowEx.Attach(FolderShadow, 20, 0.25f);
            folderShadow.RelativeSizeAdjustment = Vector2.One;

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                SettingsFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }

            ChatsList.RegisterPropertyChangedCallback(ChatsListView.SelectionMode2Property, List_SelectionModeChanged);

            var header = ElementCompositionPreview.GetElementVisual(PageHeader);
            header.Clip = header.Compositor.CreateInsetClip();

            var show = !((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats;

            ArchivedChatsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Dispose()
        {
            try
            {
                Bindings.StopTracking();

                var viewModel = ViewModel;
                if (viewModel != null)
                {
                    viewModel.Settings.Delegate = null;
                    viewModel.Chats.Delegate = null;
                    viewModel.Chats.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
                    viewModel.ArchivedChats.Delegate = null;
                    viewModel.ArchivedChats.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;

                    viewModel.Aggregator.Unsubscribe(this);
                    viewModel.Dispose();
                }

                MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated -= OnNavigated;

                MasterDetail.Dispose();
                SettingsView.Dispose();
            }
            catch { }
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            //TitleBarrr.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            //TitleBarrr.Height = sender.Height;
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 6), GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[2].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 6), GridUnitType.Pixel);

            StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            //TitleBarrr.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            //TitleBarrr.Height = sender.Height;
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 6), GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[2].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 6), GridUnitType.Pixel);

            StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        private void InitializeLocalization()
        {
            TabChats.Header = Strings.Additional.Chats;
        }

        private void InitializeLock()
        {
            Lock.Visibility = ViewModel.Passcode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            Lock.IsChecked = ViewModel.Passcode.IsLocked;
        }

        #region Handle

        public void UpdateChatLastMessage(Chat chat)
        {
            Handle(chat.Id, (chatView, chat) =>
            {
                chatView.UpdateChatReadInbox(chat);
                chatView.UpdateChatLastMessage(chat);
            });
        }

        public void Handle(UpdateChatPosition update)
        {
            if (update.Position.List is ChatListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ProtoService, ViewModel.Chats, new ChatListArchive()));
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

        public void Handle(UpdateChatTitle update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatTitle(chat));
        }

        public void Handle(UpdateChatPhoto update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatPhoto(chat));
        }

        public void Handle(UpdateUserChatAction update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatActions(chat, ViewModel.ProtoService.GetChatActions(chat.Id)));
        }

        public void Handle(UpdateUserStatus update)
        {
            if (_cacheService.TryGetChatFromUser(update.UserId, out Chat result))
            {
                var user = _cacheService.GetUser(update.UserId);
                if (user != null && user.Type is UserTypeRegular && user.Id != _cacheService.Options.MyId && user.Id != 777000)
                {
                    Handle(result.Id, (chatView, chat) => chatView.UpdateUserStatus(chat, update.Status));
                }
            }
        }

        public void Handle(UpdateMessageMentionRead update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateMessageContent update)
        {
            Handle(update.ChatId, update.MessageId, chat => chat.LastMessage.Content = update.NewContent, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
        }

        public void Handle(UpdateSecretChat update)
        {
            if (_cacheService.TryGetChatFromSecret(update.SecretChat.Id, out Chat result))
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
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ProtoService, ViewModel.Chats, update.ChatList));
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

            this.BeginOnUIThread(() =>
            {
                var chatList = GetChatListForChat(chat);
                if (chatList == null)
                {
                    return;
                }

                var container = chatList.ContainerFromItem(chat) as ListViewItem;
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
            //if (chat.Positions.Any(x => x.List is ChatListArchive))
            //{
            //    ArchivedChats.UpdateChatList(ViewModel.ProtoService, ViewModel.Chats, new ChatListArchive());
            //}

            this.BeginOnUIThread(() =>
            {
                var chatList = GetChatListForChat(chat);
                if (chatList == null)
                {
                    return;
                }

                var container = chatList.ContainerFromItem(chat) as ListViewItem;
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
                ShowHideArchive(ViewModel.SelectedFilter == null || ViewModel.SelectedFilter.ChatList is ChatListMain);

                UpdateBackButtonVisibility();
                UpdatePaneToggleButtonVisibility();
            });
        }

        public void Handle(UpdatePasscodeLock update)
        {
            this.BeginOnUIThread(() =>
            {
                Lock.Visibility = update.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
                Lock.IsChecked = update.IsLocked;
            });
        }

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                for (int i = 0; i < ViewModel.Chats.Items.Count; i++)
                {
                    var chat = ViewModel.Chats.Items[i];
                    if (chat.UpdateFile(update.File))
                    {
                        var container = ChatsList.ContainerFromIndex(i) as ListViewItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var chatView = container.ContentTemplateRoot as ChatCell;
                        if (chatView != null)
                        {
                            chatView.UpdateFile(chat, update.File);
                        }
                    }
                }

                for (int i = 0; i < ViewModel.Contacts.Items.Count; i++)
                {
                    var user = ViewModel.Contacts.Items[i];
                    if (user.UpdateFile(update.File))
                    {
                        var container = UsersListView.ContainerFromIndex(i) as ListViewItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Grid;

                        var photo = content.Children[0] as ProfilePicture;
                        photo.Source = PlaceholderHelper.GetUser(null, user, 36);
                    }
                }

                SettingsView.UpdateFile(update.File);
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

        private void SetProxyVisibility(bool expectBlocking, int proxyId, ConnectionState connectionState)
        {
            if (expectBlocking || proxyId != 0)
            {
                Proxy.Visibility = Visibility.Visible;
            }
            else
            {
                switch (connectionState)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                    case ConnectionStateConnecting connecting:
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        Proxy.Visibility = Visibility.Visible;
                        break;
                    default:
                        Proxy.Visibility = Visibility.Collapsed;
                        break;
                }
            }

            Proxy.Glyph = connectionState is ConnectionStateReady && proxyId != 0 ? "\uE916" : "\uE915";
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
            this.BeginOnUIThread(() =>
            {
                if (update.Call.IsValidState())
                {
                    CallBanner.Visibility = update.IsOpen ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    CallBanner.Visibility = Visibility.Collapsed;
                }
            });
        }

        public void Handle(UpdateChatListLayout update)
        {
            this.BeginOnUIThread(() =>
            {
                ChatsList.UpdateViewState(MasterDetail.CurrentState);
                ArchivedChats.UpdateViewState(new ChatListArchive(), false, MasterDetail.CurrentState == MasterDetailState.Compact, ((TLViewModelBase)ViewModel).Settings.UseThreeLinesLayout);
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

            if (Window.Current.Content is RootPage root && show)
            {
                root.SetTopPadding(new Thickness());
            }

            if (ChatTabs == null)
                FindName(nameof(ChatTabs));

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                // Too early, no animation needed
                ChatsList.Margin = new Thickness();

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
                    _tabsTopCollapsed = false;
                }
                else
                {
                    ChatTabs.Visibility = Visibility.Collapsed;
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

        private void ShowHideLeftTabs(bool show)
        {
            if ((show && ChatTabsLeft?.Visibility == Visibility.Visible) || (!show && (ChatTabsLeft == null || ChatTabsLeft.Visibility == Visibility.Collapsed || _tabsLeftCollapsed)))
            {
                return;
            }

            if (show)
            {
                _tabsLeftCollapsed = false;
            }

            if (Window.Current.Content is RootPage root)
            {
                root.SetTopPadding(new Thickness(show ? 72 : 0, 0, 0, 0));
            }

            if (ChatTabsLeft == null)
                FindName(nameof(ChatTabsLeft));

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
            /*if (SearchField.FocusState != FocusState.Unfocused && !string.IsNullOrEmpty(SearchField.Text))
            {
                SearchField.Text = string.Empty;
                args.Handled = true;
            }
            else*/
            if (SearchField.Visibility == Visibility.Visible)
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

        public void OnBackRequested(HandledEventArgs args)
        {
            OnBackRequesting(args);

            if (args.Handled)
            {
                return;
            }

            if (rpMasterTitlebar.SelectedIndex > 0)
            {
                rpMasterTitlebar.SelectedIndex = 0;
                args.Handled = true;
            }
            else if (FolderPanel.Visibility == Visibility.Visible)
            {
                SetFolder(new ChatListMain());
                args.Handled = true;
            }
            else if (ViewModel.Chats.Items.ChatList is ChatListFilter)
            {
                ViewModel.SelectedFilter = ChatFilterViewModel.Main;
                args.Handled = true;
            }
        }

        private ChatsPage GetChatListForChat(Chat chat)
        {
            if (chat.Positions.Any(x => x.List.ListEquals(ViewModel.Chats.Items.ChatList)))
            {
                return ChatsList;
            }
            else if (chat.Positions.Any(x => x.List.ListEquals(ViewModel.Folder?.Items.ChatList, false)))
            {
                return FolderList;
            }

            return null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Playback.Update(ViewModel.ProtoService, ViewModel.PlaybackService, ViewModel.NavigationService, ViewModel.Aggregator);

            ViewModel.Aggregator.Subscribe(this);
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += OnAcceleratorKeyActivated;

            OnStateChanged(null, null);

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
        }

        private void OnCurrentItemChanged(object sender, PropertyChangedEventArgs e)
        {
            this.BeginOnUIThread(() => ShowHideBanner(ViewModel.PlaybackService.CurrentItem != null));
        }

        private bool _bannerCollapsed;

        private async void ShowHideBanner(bool show)
        {
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

            var y = (float)Playback.ActualHeight;

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
            //var viewModel = ViewModel;
            //if (viewModel != null)
            //{
            //    viewModel.Chats.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
            //    viewModel.ArchivedChats.SelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;

            //    viewModel.Aggregator.Unsubscribe(this);
            //}

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= OnAcceleratorKeyActivated;

            var titleBar = CoreApplication.GetCurrentView().TitleBar;
            titleBar.IsVisibleChanged -= CoreTitleBar_LayoutMetricsChanged;
            titleBar.LayoutMetricsChanged -= CoreTitleBar_LayoutMetricsChanged;

            //MasterDetail.Dispose();
            //SettingsView.Dispose();

            //DataContext = null;
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
                SearchField.Text = character;
                SearchField.SelectionStart = character.Length;

                args.Handled = true;
            }
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            var commands = ViewModel.ShortcutService.Process(args);
            foreach (var command in commands)
            {
                ProcessChatCommands(command, args);
                ProcessFolderCommands(command, args);
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, AcceleratorKeyEventArgs args)
        {
            if (command == ShortcutCommand.Search)
            {
                if (MasterDetail.NavigationService.Frame.Content is ISearchablePage child && args.VirtualKey != Windows.System.VirtualKey.E)
                {
                    child.Search();
                }
                else
                {
                    Search_Click(null, null);
                }

                args.Handled = true;
            }
            else if (command == ShortcutCommand.Quit || command == ShortcutCommand.Close)
            {
                if (args.VirtualKey == Windows.System.VirtualKey.Q && App.Connection != null)
                {
                    await App.Connection.SendMessageAsync(new Windows.Foundation.Collections.ValueSet { { "Exit", string.Empty } });
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
            else if (command >= ShortcutCommand.ShowFolder1 && command <= ShortcutCommand.ShowFolder6)
            {
                var index = command - ShortcutCommand.ShowAllChats;
                if (folders.Count > index)
                {
                    ViewModel.SelectedFilter = folders[index];
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
            else if (command >= ShortcutCommand.ChatPinned1 && command <= ShortcutCommand.ChatPinned5)
            {
                var folders = ViewModel.Filters;
                if (folders.Count > 0)
                {
                    return;
                }

                var index = command - ShortcutCommand.ChatPinned1;

                var response = await ViewModel.ProtoService.SendAsync(new GetChats(new ChatListMain(), long.MaxValue, 0, ViewModel.CacheService.Options.PinnedChatCountMax * 2 + 1));
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
            if (FolderPanel.Visibility == Visibility.Visible)
            {
                SetFolder(new ChatListMain());
            }

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
                ViewModel.SelectedFilter = ViewModel.Filters[index];
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

            ArchivedChats.UpdateChatList(ViewModel.ProtoService, ViewModel.Chats, new ChatListArchive());
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

            MasterDetail.BackgroundOpacity =
                e.SourcePageType == typeof(ChatPage) ||
                e.SourcePageType == typeof(ChatThreadPage) ||
                e.SourcePageType == typeof(ChatScheduledPage) ||
                e.SourcePageType == typeof(ChatEventLogPage) ||
                e.SourcePageType == typeof(BlankPage) ||
                frame.CurrentSourcePageType == typeof(ChatPage) ||
                frame.CurrentSourcePageType == typeof(ChatThreadPage) ||
                frame.CurrentSourcePageType == typeof(ChatScheduledPage) ||
                frame.CurrentSourcePageType == typeof(ChatEventLogPage) ||
                frame.CurrentSourcePageType == typeof(BlankPage) ? 1 : 0;
        }

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            //if (e.SourcePageType == typeof(BlankPage))
            //{
            //    Grid.SetRow(Separator, 0);
            //    Separator.Visibility = Visibility.Collapsed;
            //}
            //else
            //{
            //    Grid.SetRow(Separator, 1);
            //    Separator.Visibility = Visibility.Visible;
            //}

            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                SetTitleBarVisibility(Visibility.Visible);
                MasterDetail.AllowCompact = true;
            }
            else
            {
                SetTitleBarVisibility(e.SourcePageType == typeof(BlankPage) ? Visibility.Collapsed : Visibility.Visible);
                MasterDetail.AllowCompact = e.SourcePageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == 0;
            }

            UpdatePaneToggleButtonVisibility();
            UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ViewModel.Chats.SelectedItem = null;
                    ViewModel.Chats.SelectionMode = ListViewSelectionMode.None;
                }

                Separator.BorderThickness = new Thickness(0);
                Separator.Visibility = Visibility.Collapsed;

                SetTitleBarVisibility(Visibility.Visible);
                Header.Visibility = Visibility.Visible;
                StateLabel.Visibility = Visibility.Visible;
            }
            else
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    if (ChatsList.SelectedItem2 is Chat chat)
                    {
                        ViewModel.Chats.SelectedItem = chat.Id;
                    }

                    ViewModel.Chats.SelectionMode = ListViewSelectionMode.Single;
                    //ViewModel.Chats.SelectedItem = ViewModel.Chats.SelectedItem;
                }

                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
                Separator.Visibility = Visibility.Visible;

                SetTitleBarVisibility(MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage) ? Visibility.Collapsed : Visibility.Visible);
                Header.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
                StateLabel.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdatePaneToggleButtonVisibility();

            ChatsList.UpdateViewState(MasterDetail.CurrentState);
        }

        private void UpdatePaneToggleButtonVisibility()
        {
            if (BackButton.Visibility == Visibility.Visible || SearchField.Visibility == Visibility.Visible || ChatsList.SelectionMode2 == ListViewSelectionMode.Multiple || FolderList?.SelectionMode2 == ListViewSelectionMode.Multiple)
            {
                Root?.SetPaneToggleButtonVisibility(Visibility.Collapsed);
            }
            else if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                Root?.SetPaneToggleButtonVisibility(MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage) ? Visibility.Visible : Visibility.Collapsed);
            }
            else
            {
                Root?.SetPaneToggleButtonVisibility(Visibility.Visible);
            }
        }

        private void UpdateBackButtonVisibility()
        {
            if (rpMasterTitlebar.SelectedIndex != 0 || FolderPanel.Visibility == Visibility.Visible || SearchField.Visibility == Visibility.Visible)
            {
                BackButton.Visibility = Visibility.Visible;
            }
            else
            {
                BackButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateListViewsSelectedItem(long chatId)
        {
            ViewModel.Chats.SelectedItem = chatId;

            var dialog = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == chatId);
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                ChatsList.SelectedItem2 = dialog;
            }

            var folder = ViewModel.Folder;
            if (folder == null)
            {
                return;
            }

            folder.SelectedItem = chatId;

            dialog = folder.Items.FirstOrDefault(x => x.Id == chatId);
            if (folder.SelectionMode != ListViewSelectionMode.Multiple)
            {
                FolderList.SelectedItem2 = dialog;
            }
        }

        private void SetTitleBarVisibility(Visibility visibility)
        {
            MasterDetail.IsBlank = visibility == Visibility.Collapsed;
            VisualStateManager.GoToState(this, visibility == Visibility.Collapsed ? "Normal" : "TitleBar", false);
        }

        public Visibility EvalutatePaneToggleButtonVisibility()
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                return MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
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

                var folder = ViewModel.Folder;
                if (folder != null)
                {
                    folder.SelectedItem = chat.Id;
                }

                MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                MasterDetail.NavigationService.GoBackAt(0, false);
            }
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateManage();

            var listView = sender as ListView;
            if (listView.SelectedItem != null)
            {
                //listView.ScrollIntoView(listView.SelectedItem);
            }
            else
            {
                // Find another solution
                await Task.Delay(500);
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
            }
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateManage();
        }

        private void InitializeSearch()
        {
            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(async x =>
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
            });
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
                    Root?.SetSelectedIndex(RootDestination.Contacts);
                    break;
                case 2:
                    Root?.SetSelectedIndex(RootDestination.Calls);
                    break;
                case 3:
                    Root?.SetSelectedIndex(RootDestination.Settings);
                    break;
            }


            SearchField.Text = string.Empty;
            SearchField.Visibility = Visibility.Collapsed;

            UpdateBackButtonVisibility();

            UpdateHeader();
            UpdatePaneToggleButtonVisibility();

            try
            {
                SearchReset();
                rpMasterTitlebar.IsLocked = false;
            }
            catch { }

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
        }

        private void UpdateHeader()
        {
            ChatsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ContactsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            SettingsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

            SearchField.PlaceholderText = rpMasterTitlebar.SelectedIndex == 3 ? Strings.Resources.SearchInSettings : Strings.Resources.Search;
            ChatTabsSearch.Content = rpMasterTitlebar.SelectedIndex == 3 ? Strings.Resources.SearchInSettings : Strings.Resources.Search;
        }

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if ((show && DialogsPanel.Visibility == Visibility.Collapsed) || (!show && (DialogsPanel.Visibility == Visibility.Visible || _searchCollapsed)))
            {
                return;
            }

            if (show)
            {
                _searchCollapsed = false;
            }
            else
            {
                _searchCollapsed = true;
            }

            DialogsPanel.Visibility = Visibility.Visible;

            var chats = ElementCompositionPreview.GetElementVisual(DialogsPanel);
            var panel = ElementCompositionPreview.GetElementVisual(DialogsSearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3((float)DialogsPanel.ActualWidth / 2, (float)DialogsPanel.ActualHeight / 2, 0);

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
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;
            UpdateBackButtonVisibility();
            UpdatePaneToggleButtonVisibility();

            SearchField.Focus(FocusState.Keyboard);
            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Visible;
            SearchField.Visibility = Visibility.Collapsed;
            UpdateBackButtonVisibility();
            UpdatePaneToggleButtonVisibility();

            rpMasterTitlebar.IsLocked = false;
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && rpMasterTitlebar.SelectedIndex == 0;

            SearchReset();
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrWhiteSpace(SearchField.Text))
            {
                return;
            }

            rpMasterTitlebar.IsLocked = true;
            MasterDetail.AllowCompact = false;

            if (rpMasterTitlebar.SelectedIndex == 0)
            {
                //DialogsPanel.Visibility = Visibility.Collapsed;
                ShowHideSearch(true);

                if (ViewModel.Chats.SearchFilters.IsEmpty() && string.IsNullOrEmpty(SearchField.Text))
                {
                    var top = ViewModel.Chats.TopChats = new TopChatsCollection(ViewModel.ProtoService, new TopChatCategoryUsers(), 30);
                    await top.LoadMoreItemsAsync(0);
                }
                else
                {
                    ViewModel.Chats.TopChats = null;
                }

                var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters, FolderPanel.Visibility == Visibility.Collapsed ? null : new ChatListArchive());
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
                    SettingsView.Visibility = Visibility.Collapsed;

                    ViewModel.Settings.Search(SearchField.Text);
                }
            }
        }

        private void SearchReset()
        {
            //DialogsPanel.Visibility = Visibility.Visible;
            ShowHideSearch(false);
            ContactsPanel.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Visible;

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

                        var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters, FolderPanel.Visibility == Visibility.Collapsed ? null : new ChatListArchive());
                        await items.LoadMoreItemsAsync(0);
                        await items.LoadMoreItemsAsync(1);
                        await items.LoadMoreItemsAsync(2);
                        await items.LoadMoreItemsAsync(3);
                        await items.LoadMoreItemsAsync(4);

                        return;
                    }
                }
            }

            var activePanel = rpMasterTitlebar.SelectedIndex == 0 ? DialogsPanel : ContactsPanel;
            var activeList = rpMasterTitlebar.SelectedIndex == 0 ? DialogsSearchListView : ContactsSearchListView;
            var activeResults = rpMasterTitlebar.SelectedIndex == 0 ? ChatsResults : ContactsResults;

            if (activePanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
            {
                var index = e.Key == Windows.System.VirtualKey.Up ? -1 : 1;
                var next = activeList.SelectedIndex + index;
                if (next >= 0 && next < activeResults.View.Count)
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

            Lock.IsChecked = !Lock.IsChecked;

            if (Lock.IsChecked == true)
            {
                ViewModel.Passcode.Lock();

                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    App.ShowPasscode();
                }

                Automation.SetToolTip(Lock, Strings.Resources.AccDescrPasscodeUnlock);
            }
            else
            {
                Automation.SetToolTip(Lock, Strings.Resources.AccDescrPasscodeLock);
            }
        }

        private void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            SettingsView.EditPhoto_Click(sender, e);
        }

        private void OnUpdate(object sender, EventArgs e)
        {
            Bindings.Update();
        }

        private void DialogsSearchListView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = DialogsSearchListView.ItemContainerStyle;
            }

            args.ItemContainer.ContentTemplate = DialogsSearchListView.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void DialogsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is SearchResult result)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                if (args.Phase == 0)
                {
                    var grid = content.Children[1] as Grid;

                    var title = grid.Children[0] as TextBlock;
                    title.Style = App.Current.Resources[result?.Chat?.Type is ChatTypeSecret ? "SecretBodyTextBlockStyle" : "BodyTextBlockStyle"] as Style;

                    if (result.Chat != null)
                    {
                        title.Text = ViewModel.ProtoService.GetTitle(result.Chat);
                    }
                    else if (result.User != null)
                    {
                        title.Text = result.User.GetFullName();
                    }

                    var verified = grid.Children[1] as FrameworkElement;

                    if (result.User != null || result.Chat.Type is ChatTypePrivate || result.Chat.Type is ChatTypeSecret)
                    {
                        var user = result.User ?? ViewModel.ProtoService.GetUser(result.Chat);
                        verified.Visibility = user != null && user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup supergroup)
                    {
                        var group = ViewModel.ProtoService.GetSupergroup(supergroup.SupergroupId);
                        verified.Visibility = group != null && group.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        verified.Visibility = Visibility.Collapsed;
                    }
                }
                else if (args.Phase == 1)
                {
                    var subtitle = content.Children[2] as TextBlock;
                    if (result.User != null || result.Chat != null && (result.Chat.Type is ChatTypePrivate privata || result.Chat.Type is ChatTypeSecret))
                    {
                        var user = result.User ?? ViewModel.ProtoService.GetUser(result.Chat);
                        if (result.IsPublic)
                        {
                            subtitle.Text = $"@{user.Username}";
                        }
                        else
                        {
                            subtitle.Text = LastSeenConverter.GetLabel(user, true);
                        }
                    }
                    else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup super)
                    {
                        var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                        if (result.IsPublic)
                        {
                            if (supergroup.MemberCount > 0)
                            {
                                subtitle.Text = string.Format("@{0}, {1}", supergroup.Username, Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount));
                            }
                            else
                            {
                                subtitle.Text = $"@{supergroup.Username}";
                            }
                        }
                        else if (supergroup.MemberCount > 0)
                        {
                            subtitle.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
                        }
                        else
                        {
                            subtitle.Text = string.Empty;
                        }
                    }
                    else
                    {
                        subtitle.Text = string.Empty;
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
                    if (result.Chat != null)
                    {
                        photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, result.Chat, 36);
                    }
                    else if (result.User != null)
                    {
                        photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, result.User, 36);
                    }
                }

                if (args.Phase < 2)
                {
                    args.RegisterUpdateCallback(DialogsSearchListView_ContainerContentChanging);
                }
            }
            else if (args.Item is Message message)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
                if (content == null)
                {
                    return;
                }

                content.UpdateMessage(ViewModel.ProtoService, ViewModel.Chats, message);
            }

            args.Handled = true;
        }

        private void UsersListView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = UsersListView.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = UsersListView.ItemTemplate;
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

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var user = args.Item as User;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = LastSeenConverter.GetLabel(user, false);
                subtitle.Foreground = App.Current.Resources[user.Status is UserStatusOnline ? "SystemControlForegroundAccentBrush" : "SystemControlDisabledChromeDisabledLowBrush"] as Brush;
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(UsersListView_ContainerContentChanging);
            }

            args.Handled = true;
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
            button.Glyph = entry.Glyph ?? string.Empty;

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
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
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

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 48);
            title.Text = ViewModel.ProtoService.GetTitle(chat, true);

            var badge = grid.Children[1] as Border;
            var text = badge.Child as TextBlock;

            badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            text.Text = chat.UnreadCount.ToString();

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

        public async void NavigationView_ItemClick(RootDestination destination)
        {
            if (destination == RootDestination.NewChat)
            {
                MasterDetail.NavigationService.Navigate(typeof(BasicGroupCreateStep1Page));
            }
            else if (destination == RootDestination.NewSecretChat)
            {
                ViewModel.CreateSecretChatCommand.Execute();
            }
            else if (destination == RootDestination.NewChannel)
            {
                MasterDetail.NavigationService.Navigate(typeof(ChannelCreateStep1Page));
            }
            else if (destination == RootDestination.Chats)
            {
                rpMasterTitlebar.SelectedIndex = 0;
                MasterDetail.Push(true);
            }
            else if (destination == RootDestination.Contacts)
            {
                rpMasterTitlebar.SelectedIndex = 1;
                MasterDetail.Push(true);
            }
            else if (destination == RootDestination.Calls)
            {
                rpMasterTitlebar.SelectedIndex = 2;
                MasterDetail.Push(true);
            }
            else if (destination == RootDestination.Settings)
            {
                rpMasterTitlebar.SelectedIndex = 3;
                MasterDetail.Push(true);
            }
            else if (destination == RootDestination.InviteFriends)
            {
                MasterDetail.NavigationService.Navigate(typeof(InvitePage));
            }
            else if (destination == RootDestination.SavedMessages)
            {
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(ViewModel.CacheService.Options.MyId, false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                }
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ProtoService, MasterDetail.NavigationService, "unigram", null, null, null, null);
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

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(LogOutPage));
        }

        private void EditName_Click(object sender, RoutedEventArgs e)
        {
            SettingsView.EditName_Click(sender, e);
        }

        private ChatFilterViewModel ConvertFilter(ChatFilterViewModel filter)
        {
            ShowHideTopTabs(!ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0);
            ShowHideLeftTabs(ViewModel.Chats.Settings.IsLeftTabsEnabled && ViewModel.Filters.Count > 0);
            ShowHideArchive(filter == null || filter.ChatList is ChatListMain);

            UpdateBackButtonVisibility();
            UpdatePaneToggleButtonVisibility();

            return filter;
        }

        private void ConvertFilterBack(object obj)
        {
            if (obj is ChatFilterViewModel filter)
            {
                ViewModel.SelectedFilter = filter;
                ConvertFilter(filter);
            }
        }

        private void SetFolder(ChatList chatList)
        {
            var hide = chatList is ChatListMain || chatList == null;
            var show = !hide;

            ViewModel.SetFolder(chatList);

            if (chatList is ChatListMain || chatList == null)
            {
                rpMasterTitlebar.IsLocked = false;
                ChatTabsSearch.Content = Strings.Resources.Search;
            }
            else if (chatList is ChatListArchive)
            {
                rpMasterTitlebar.IsLocked = true;
                ChatTabsSearch.Content = Strings.Resources.ArchivedChats;
            }

            FolderPanel.Visibility = Visibility.Visible;

            UpdateBackButtonVisibility();
            UpdateHeader();
            UpdatePaneToggleButtonVisibility();

            var chats = ElementCompositionPreview.GetElementVisual(ChatsPanel);
            var folder = ElementCompositionPreview.GetElementVisual(FolderList);
            var shadow = ElementCompositionPreview.GetElementVisual(FolderShadow);

            var anim1 = chats.Compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(show ? 0 : 1, 0);
            anim1.InsertKeyFrame(show ? 1 : 0, -(float)(DialogsPanel.ActualWidth / 3));

            var anim2 = chats.Compositor.CreateScalarKeyFrameAnimation();
            anim2.InsertKeyFrame(show ? 0 : 1, (float)DialogsPanel.ActualWidth);
            anim2.InsertKeyFrame(show ? 1 : 0, 0);

            var anim3 = chats.Compositor.CreateScalarKeyFrameAnimation();
            anim3.InsertKeyFrame(show ? 0 : 1, 0);
            anim3.InsertKeyFrame(show ? 1 : 0, 1);

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                FolderPanel.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                chats.Offset = new Vector3();
                folder.Offset = new Vector3();
                shadow.Opacity = 0;

                UpdateBackButtonVisibility();
                UpdateHeader();
                UpdatePaneToggleButtonVisibility();
            };

            chats.StartAnimation("Offset.X", anim1);
            folder.StartAnimation("Offset.X", anim2);
            shadow.StartAnimation("Opacity", anim3);
            batch.End();
        }

        public void ArchivedChats_Click(object sender, RoutedEventArgs e)
        {
            SetFolder(new ChatListArchive());
        }

        private void ChatFilter_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var filter = ChatFilters?.ItemFromContainer(sender) as ChatFilterViewModel;

            if (filter == null)
            {
                filter = ChatFiltersSide.ItemFromContainer(sender) as ChatFilterViewModel;
            }

            if (filter.ChatFilterId == Constants.ChatListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.FilterEditCommand, filter, Strings.Resources.FilterEditAll, new FontIcon { Glyph = Icons.Edit });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.FilterEditCommand, filter, Strings.Resources.FilterEdit, new FontIcon { Glyph = Icons.Edit });
                flyout.CreateFlyoutItem(ViewModel.FilterAddCommand, filter, Strings.Resources.FilterAddChats, new FontIcon { Glyph = Icons.Add });
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.FilterDeleteCommand, filter, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "TopEdgeAlignedRight"))
            {
                flyout.ShowAt(element, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
            }
            else
            {
                flyout.ShowAt(element);
            }
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

            if (((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats)
            {
                flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.Resources.AccDescrExpandPanel, new FontIcon { Glyph = "\uF164" });
            }
            else
            {
                flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.Resources.AccDescrCollapsePanel, new FontIcon { Glyph = "\uF166" });
            }

            args.ShowAt(flyout, element);
        }

        private async void ToggleArchive()
        {
            ViewModel.ToggleArchiveCommand.Execute();

            ArchivedChatsPanel.Visibility = Visibility.Visible;
            ArchivedChatsCompactPanel.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            var show = !((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;

            var presenter = ElementCompositionPreview.GetElementVisual(ArchivedChatsPresenter);
            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var chats = ElementCompositionPreview.GetElementVisual(element);
            var panel = ElementCompositionPreview.GetElementVisual(ArchivedChatsPanel);
            var compact = ElementCompositionPreview.GetElementVisual(ArchivedChatsCompactPanel);

            presenter.Clip = chats.Compositor.CreateInsetClip();
            parent.Clip = chats.Compositor.CreateInsetClip();

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                panel.Offset = new Vector3();
                compact.Offset = new Vector3();

                ArchivedChatsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                ChatsList.Margin = new Thickness();
            };

            var panelY = (float)ArchivedChatsPanel.ActualHeight;
            var compactY = (float)ArchivedChatsCompactPanel.ActualHeight;

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

            var offset1 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(0, new Vector3(0, show ? 0 : compactY, 0));
            offset1.InsertKeyFrame(1, new Vector3(0, show ? compactY : 0, 0));
            compact.StartAnimation("Offset", offset1);

            var offset2 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(0, show ? -compactY : 0, 0));
            offset2.InsertKeyFrame(1, new Vector3(0, show ? 0 : -compactY, 0));
            panel.StartAnimation("Offset", offset2);

            batch.End();
        }

        private bool _archiveCollapsed;

        private async void ShowHideArchive(bool show)
        {
            if ((show && ArchivedChatsPresenter.Visibility == Visibility.Visible) || (!show && (ArchivedChatsPresenter.Visibility == Visibility.Collapsed || _archiveCollapsed)))
            {
                return;
            }

            if (show)
            {
                _archiveCollapsed = false;
            }
            else
            {
                _archiveCollapsed = true;
            }

            ArchivedChatsPresenter.Visibility = Visibility.Visible;
            await ArchivedChatsPanel.UpdateLayoutAsync();

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;

            var parent = ElementCompositionPreview.GetElementVisual(ChatsList);

            var chats = ElementCompositionPreview.GetElementVisual(element);
            var panel = ElementCompositionPreview.GetElementVisual(ArchivedChatsPanel);
            var compact = ElementCompositionPreview.GetElementVisual(ArchivedChatsCompactPanel);

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

            var y = (float)ArchivedChatsPresenter.ActualHeight;

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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.Visibility == Visibility.Visible)
            {
                Search_LostFocus(null, null);
            }
            else if (rpMasterTitlebar.SelectedIndex > 0)
            {
                rpMasterTitlebar.SelectedIndex = 0;
            }
            else if (FolderPanel.Visibility == Visibility.Visible)
            {
                SetFolder(new ChatListMain());
            }
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

        private void ShowHideManagePanel(bool show)
        {
            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            var info = ElementCompositionPreview.GetElementVisual(MainHeader);

            manage.StopAnimation("Offset");
            manage.StopAnimation("Opacity");
            info.StopAnimation("Offset");
            info.StopAnimation("Opacity");

            if ((show && MainHeader.Visibility == Visibility.Collapsed) || (!show && ManagePanel.Visibility == Visibility.Collapsed))
            {
                return;
            }

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

        public void SetSelectedItem(Chat chat)
        {
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                ChatsList.SelectedItem2 = chat;
            }
        }

        public void SetSelectedItems()
        {
            ChatsList.SetSelectedItems();

            //if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            //{
            //    foreach (var item in chats)
            //    {
            //        if (!ChatsList.SelectedItems.Contains(item))
            //        {
            //            ChatsList.SelectedItems.Add(item);
            //        }
            //    }

            //    foreach (Chat item in ChatsList.SelectedItems)
            //    {
            //        if (!chats.Contains(item))
            //        {
            //            ChatsList.SelectedItems.Remove(item);
            //        }
            //    }
            //}
        }

        public bool IsItemSelected(Chat chat)
        {
            return ViewModel.Chats.SelectedItems.Contains(chat);
        }

        private void UpdateManage()
        {
            if (ViewModel.Chats.SelectedItems.Count > 0)
            {
                var muted = ViewModel.Chats.SelectedItems.Any(x => ViewModel.CacheService.GetNotificationSettingsMuteFor(x) > 0);
                ManageMute.Glyph = muted ? Icons.Unmute : Icons.Mute;
                Automation.SetToolTip(ManageMute, muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications);

                var unread = ViewModel.Chats.SelectedItems.Any(x => x.IsUnread());
                ManageMark.Glyph = unread ? Icons.MarkAsRead : Icons.MarkAsUnread;
                Automation.SetToolTip(ManageMark, unread ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread);

                ManageClear.IsEnabled = ViewModel.Chats.SelectedItems.All(x => ChatsList.DialogClear_Loaded(x));
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
                return Icons.Unmute;
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
                return Icons.Contact;
            }
            else if (filter == ChatListFilterFlags.IncludeNonContacts)
            {
                return Icons.Help;
            }
            else if (filter == ChatListFilterFlags.IncludeGroups)
            {
                return Icons.Group;
            }
            else if (filter == ChatListFilterFlags.IncludeChannels)
            {
                return Icons.Channel;
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

            if (e.Items.Count > 1 || e.Items[0] is ChatFilterViewModel filter && filter.ChatList is ChatListMain)
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Filters;
                if (items == null || items.Count < 3)
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
                if (compare.ChatList is ChatListMain && index > 0)
                {
                    compare = items[index + 1];
                }

                if (compare.ChatList is ChatListFilter)
                {
                    ViewModel.ProtoService.Send(new ReorderChatFilters(items.Where(x => x.ChatList is ChatListFilter).Select(x => x.ChatFilterId).ToArray()));
                }
                else
                {
                    ViewModel.Handle(new UpdateChatFilters(ViewModel.CacheService.ChatFilters));
                }
            }
        }

        private void ChatFilter_ItemClick(object sender, ItemClickEventArgs e)
        {
            rpMasterTitlebar.SelectedIndex = 0;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal &&
                MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage))
            {
                MasterDetail.NavigationService.GoBackAt(0);
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

                var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.Chats.SearchFilters, FolderPanel.Visibility == Visibility.Collapsed ? null : new ChatListArchive());
                await items.LoadMoreItemsAsync(0);
                await items.LoadMoreItemsAsync(1);
                await items.LoadMoreItemsAsync(2);
                await items.LoadMoreItemsAsync(3);
                await items.LoadMoreItemsAsync(4);
            }
        }
    }

    public class HostedPage : Page
    {
        public UIElement Header
        {
            get { return (UIElement)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(UIElement), typeof(HostedPage), new PropertyMetadata(null));
    }

    public class HostedUserControl : UserControl
    {
        public UIElement Header
        {
            get { return (UIElement)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(UIElement), typeof(HostedUserControl), new PropertyMetadata(null));
    }
}
