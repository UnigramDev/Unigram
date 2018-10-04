using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Unigram.Views.Channels;
using Unigram.Views.Chats;
using Unigram.Views.Passport;
using Unigram.Views.SecretChats;
using Unigram.Views.Settings;
using Unigram.Views.Users;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
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
        INavigablePage,
        IHandle<UpdateChatDraftMessage>,
        IHandle<UpdateChatLastMessage>,
        IHandle<UpdateChatIsPinned>,
        IHandle<UpdateChatIsMarkedAsUnread>,
        IHandle<UpdateChatReadInbox>,
        IHandle<UpdateChatReadOutbox>,
        IHandle<UpdateChatUnreadMentionCount>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>,
        IHandle<UpdateMessageMentionRead>,
        //IHandle<UpdateMessageContent>,
        IHandle<UpdateSecretChat>,
        IHandle<UpdateChatNotificationSettings>,
        IHandle<UpdateWorkMode>,
        IHandle<UpdatePasscodeLock>,
        IHandle<UpdateFile>,
        IHandle<UpdateConnectionState>,
        IHandle<UpdateOption>,
        IHandle<UpdateCall>
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        public RootPage Root { get; set; }

        private readonly ICacheService _cacheService;

        private object _lastSelected;
        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MainViewModel>();

            _cacheService = ViewModel.CacheService;

            SettingsView.DataContext = ViewModel.Settings;
            ViewModel.Settings.Delegate = SettingsView;

            NavigationCacheMode = NavigationCacheMode.Enabled;

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

            var separator = ElementCompositionPreview.GetElementVisual(Separator);
            var visual = Shadow.Attach(Separator, 20, 0.25f, separator.Compositor.CreateInsetClip(-100, 0, 19, 0));

            Separator.SizeChanged += (s, args) =>
            {
                visual.Size = new Vector2(20, (float)args.NewSize.Height);
            };
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            TitleBarrr.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            TitleBarrr.Height = sender.Height;
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(sender.SystemOverlayLeftInset, GridUnitType.Pixel);

            //PageHeader.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
            MasterDetail.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            TitleBarrr.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            TitleBarrr.Height = sender.Height;
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(sender.SystemOverlayLeftInset, GridUnitType.Pixel);

            //PageHeader.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
            MasterDetail.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
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

        public void Handle(UpdateChatDraftMessage update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
        }

        public void Handle(UpdateChatLastMessage update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
        }

        public void Handle(UpdateChatIsPinned update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
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

        public void Handle(UpdateWorkMode update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.IsVisible)
                {
                    WorkMode.Visibility = Visibility.Visible;
                    WorkMode.IsChecked = update.IsEnabled;
                }
                else
                {
                    WorkMode.Visibility = Visibility.Collapsed;
                    WorkMode.IsChecked = false;
                }
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
                        photo.Source = PlaceholderHelper.GetUser(null, user, 36, 36);
                    }
                }

                SettingsView.UpdateFile(update.File);
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
                        ShowStatus(Strings.Resources.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting connecting:
                        ShowStatus(Strings.Resources.Connecting);
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        ShowStatus(Strings.Resources.ConnectingToProxy);
                        break;
                    case ConnectionStateUpdating updating:
                        ShowStatus(Strings.Resources.Updating);
                        break;
                    case ConnectionStateReady ready:
                        HideStatus();
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
        }

        private void ShowStatus(string text)
        {
            Status.IsIndeterminate = true;
            StatusLabel.Text = text;
        }

        private void HideStatus()
        {
            Status.IsIndeterminate = false;
            StatusLabel.Text = "Unigram";
        }

        public void Handle(UpdateCall update)
        {
            this.BeginOnUIThread(() =>
            {
                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                    case CallStateError error:
                        CallBanner.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        CallBanner.Visibility = Visibility.Visible;
                        break;
                }
            });
        }

        private void Handle(long chatId, long messageId, Action<Chat> update, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                var chat = ViewModel.ProtoService.GetChat(chatId);
                if (chat.LastMessage == null || chat.LastMessage.Id != messageId)
                {
                    return;
                }

                var container = ChatsList.ContainerFromItem(chat) as ListViewItem;
                if (container == null)
                {
                    return;
                }

                update(chat);

                var chatView = container.ContentTemplateRoot as ChatCell;
                if (chatView != null)
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(long chatId, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                var chat = ViewModel.ProtoService.GetChat(chatId);
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

        #endregion

        public void OnBackRequested(HandledEventArgs args)
        {
            if (rpMasterTitlebar.SelectedIndex > 0)
            {
                rpMasterTitlebar.SelectedIndex = 0;
                args.Handled = true;
            }
            else if (SearchField.FocusState != FocusState.Unfocused && !string.IsNullOrEmpty(SearchField.Text))
            {
                SearchField.Text = string.Empty;
                args.Handled = true;
            }
            else if (SearchField.FocusState != FocusState.Unfocused)
            {
                DialogsSearchListView.Focus(FocusState.Programmatic);
                args.Handled = true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += OnAcceleratorKeyActivated;

            OnStateChanged(null, null);

            if (_unloaded)
            {
                _unloaded = false;
                ChatsList.ItemsSource = null;

                Bindings.StopTracking();
                Bindings.Update();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= OnAcceleratorKeyActivated;

            Bindings.StopTracking();

            _unloaded = true;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType != CoreAcceleratorKeyEventType.KeyDown && args.EventType != CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                return;
            }

            var alt = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var ctrl = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if ((args.VirtualKey == Windows.System.VirtualKey.Up && alt) || (args.VirtualKey == Windows.System.VirtualKey.PageUp && ctrl) || (args.VirtualKey == Windows.System.VirtualKey.Tab && ctrl && shift))
            {
                Scroll(true, true);
                args.Handled = true;
            }
            else if ((args.VirtualKey == Windows.System.VirtualKey.Down && alt) || (args.VirtualKey == Windows.System.VirtualKey.PageDown && ctrl) || (args.VirtualKey == Windows.System.VirtualKey.Tab && ctrl && !shift))
            {
                Scroll(false, true);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Up && !alt && !ctrl && !shift && !MasterDetail.NavigationService.CanGoBack && SearchField.FocusState == FocusState.Unfocused)
            {
                Scroll(true, false);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Down && !alt && !ctrl && !shift && !MasterDetail.NavigationService.CanGoBack && SearchField.FocusState == FocusState.Unfocused)
            {
                Scroll(false, false);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Home && !alt && !ctrl && !shift)
            {
                ChatsList.ScrollIntoView(ViewModel.Chats.Items.FirstOrDefault());
            }
            else if (((args.VirtualKey == Windows.System.VirtualKey.E || args.VirtualKey == Windows.System.VirtualKey.K) && ctrl && !alt && !shift) || args.VirtualKey == Windows.System.VirtualKey.Search)
            {
                MasterDetail.AllowCompact = false;

                Header.Visibility = Visibility.Visible;
                MainHeader.Visibility = Visibility.Collapsed;
                SearchField.Visibility = Visibility.Visible;

                SearchField.Focus(FocusState.Keyboard);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Q && ctrl && !alt && !shift)
            {
                Application.Current.Exit();
            }
        }

        public void Scroll(bool up, bool navigate)
        {
            var index = ChatsList.SelectedIndex;
            if (up)
            {
                index--;
            }
            else
            {
                index++;
            }

            if (index >= 0 && index < ViewModel.Chats.Items.Count)
            {
                ChatsList.SelectedIndex = index;

                if (navigate)
                {
                    Navigate(ChatsList.SelectedItem);
                }
            }
            else if (index < 0 && up && !navigate)
            {
                Search_Click(null, null);
            }
        }

        public void Initialize()
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main", Frame, ViewModel.ProtoService.SessionId);
                MasterDetail.NavigationService.Frame.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
            ViewModel.Chats.NavigationService = MasterDetail.NavigationService;
            ViewModel.Contacts.NavigationService = MasterDetail.NavigationService;
            ViewModel.Calls.NavigationService = MasterDetail.NavigationService;
            ViewModel.Settings.NavigationService = MasterDetail.NavigationService;

            if (((TLViewModelBase)ViewModel).Settings.IsWorkModeVisible)
            {
                WorkMode.Visibility = Visibility.Visible;
                WorkMode.IsChecked = ((TLViewModelBase)ViewModel).Settings.IsWorkModeEnabled;
            }
            else
            {
                WorkMode.Visibility = Visibility.Collapsed;
                WorkMode.IsChecked = false;
            }
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
                        MasterDetail.NavigationService.NavigateToChat(chat);
                    }
                }
                else if (data.ContainsKey("chat_id") && int.TryParse(data["chat_id"], out int chat_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreateBasicGroupChat(chat_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat);
                    }
                }
                else if (data.ContainsKey("channel_id") && int.TryParse(data["channel_id"], out int channel_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreateSupergroupChat(channel_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat);
                    }
                }
            }
        }

        public async void Activate(Uri scheme)
        {
            if (MessageHelper.IsTelegramUrl(scheme))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ProtoService, MasterDetail.NavigationService, scheme.ToString());
            }
            else if (scheme.Scheme.Equals("ms-contact-profile") || scheme.Scheme.Equals("ms-ipmessaging"))
            {
                var query = scheme.Query.ParseQueryString();
                if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat);
                    }
                }
            }
            else
            {
                string username = null;
                string group = null;
                string sticker = null;
                string[] instantView = null;
                Dictionary<string, object> auth = null;
                string botUser = null;
                string botChat = null;
                string message = null;
                string phone = null;
                string game = null;
                string phoneHash = null;
                string post = null;
                string server = null;
                string port = null;
                string user = null;
                string pass = null;
                string secret = null;
                bool hasUrl = false;

                var query = scheme.Query.ParseQueryString();
                if (scheme.AbsoluteUri.StartsWith("tg:resolve") || scheme.AbsoluteUri.StartsWith("tg://resolve"))
                {
                    username = query.GetParameter("domain");

                    if (string.Equals(username, "telegrampassport", StringComparison.OrdinalIgnoreCase))
                    {
                        username = null;
                        auth = new Dictionary<string, object>();
                        var scope = query.GetParameter("scope");
                        if (!string.IsNullOrEmpty(scope) && scope.StartsWith("{") && scope.EndsWith("}"))
                        {
                            auth.Add("nonce", query.GetParameter("nonce"));
                        }
                        else
                        {
                            auth.Add("payload", query.GetParameter("payload"));
                        }

                        auth.Add("bot_id", int.Parse(query.GetParameter("bot_id")));
                        auth.Add("scope", scope);
                        auth.Add("public_key", query.GetParameter("public_key"));
                        auth.Add("callback_url", query.GetParameter("callback_url"));
                    }
                    else
                    {
                        botUser = query.GetParameter("start");
                        botChat = query.GetParameter("startgroup");
                        game = query.GetParameter("game");
                        post = query.GetParameter("post");
                    }
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:join") || scheme.AbsoluteUri.StartsWith("tg://join"))
                {
                    group = query.GetParameter("invite");
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:addstickers") || scheme.AbsoluteUri.StartsWith("tg://addstickers"))
                {
                    sticker = query.GetParameter("set");
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:msg") || scheme.AbsoluteUri.StartsWith("tg://msg") || scheme.AbsoluteUri.StartsWith("tg://share") || scheme.AbsoluteUri.StartsWith("tg:share"))
                {
                    message = query.GetParameter("url");
                    if (message == null)
                    {
                        message = "";
                    }
                    if (query.GetParameter("text") != null)
                    {
                        if (message.Length > 0)
                        {
                            hasUrl = true;
                            message += "\n";
                        }
                        message += query.GetParameter("text");
                    }
                    if (message.Length > 4096 * 4)
                    {
                        message = message.Substring(0, 4096 * 4);
                    }
                    while (message.EndsWith("\n"))
                    {
                        message = message.Substring(0, message.Length - 1);
                    }
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:confirmphone") || scheme.AbsoluteUri.StartsWith("tg://confirmphone"))
                {
                    phone = query.GetParameter("phone");
                    phoneHash = query.GetParameter("hash");
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:passport") || scheme.AbsoluteUri.StartsWith("tg://passport") || scheme.AbsoluteUri.StartsWith("tg:secureid") || scheme.AbsoluteUri.StartsWith("tg://secureid"))
                {
                    //url = url.replace("tg:passport", "tg://telegram.org").replace("tg://passport", "tg://telegram.org").replace("tg:secureid", "tg://telegram.org");
                    //data = Uri.parse(url);
                    //auth = new HashMap<>();
                    //String scope = data.getQueryParameter("scope");
                    //if (!TextUtils.isEmpty(scope) && scope.startsWith("{") && scope.endsWith("}"))
                    //{
                    //    auth.put("nonce", data.getQueryParameter("nonce"));
                    //}
                    //else
                    //{
                    //    auth.put("payload", data.getQueryParameter("payload"));
                    //}
                    //auth.put("bot_id", data.getQueryParameter("bot_id"));
                    //auth.put("scope", scope);
                    //auth.put("public_key", data.getQueryParameter("public_key"));
                    //auth.put("callback_url", data.getQueryParameter("callback_url"));
                }
                else if (scheme.AbsoluteUri.StartsWith("tg:socks") || scheme.AbsoluteUri.StartsWith("tg://socks") || scheme.AbsoluteUri.StartsWith("tg:proxy") || scheme.AbsoluteUri.StartsWith("tg://proxy"))
                {
                    server = query.GetParameter("server");
                    port = query.GetParameter("port");
                    user = query.GetParameter("user");
                    pass = query.GetParameter("pass");
                    secret = query.GetParameter("secret");
                }

                if (message != null && message.StartsWith("@"))
                {
                    message = " " + message;
                }

                if (phone != null || phoneHash != null)
                {
                    MessageHelper.NavigateToConfirmPhone(ViewModel.ProtoService, phone, phoneHash);
                }
                if (server != null && int.TryParse(port, out int portCode))
                {
                    MessageHelper.NavigateToProxy(ViewModel.ProtoService, server, portCode, user, pass, secret);
                }
                else if (group != null)
                {
                    MessageHelper.NavigateToInviteLink(ViewModel.ProtoService, MasterDetail.NavigationService, group);
                }
                else if (sticker != null)
                {
                    MessageHelper.NavigateToStickerSet(sticker);
                }
                else if (username != null)
                {
                    MessageHelper.NavigateToUsername(ViewModel.ProtoService, MasterDetail.NavigationService, username, botUser ?? botChat, post, game);
                }
                else if (message != null)
                {
                    MessageHelper.NavigateToShare(message, hasUrl);
                }
                else if (auth != null)
                {
                    MasterDetail.NavigationService.Navigate(typeof(PassportPage), state: auth);
                }
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
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
                Root?.SetPaneToggleButtonVisibility(e.SourcePageType == typeof(BlankPage) ? Visibility.Visible : Visibility.Collapsed);
                SetTitleBarVisibility(Visibility.Visible);
            }
            else
            {
                Root?.SetPaneToggleButtonVisibility(Visibility.Visible);
                SetTitleBarVisibility(e.SourcePageType == typeof(BlankPage) ? Visibility.Collapsed : Visibility.Visible);
            }

            if (e.SourcePageType == typeof(ChatPage))
            {
                var parameter = MasterDetail.NavigationService.SerializationService.Deserialize((string)e.Parameter);
                UpdateListViewsSelectedItem((long)parameter);
            }
            else
            {
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                ChatsList.SelectionMode = ListViewSelectionMode.None;
                ChatsList.SelectedItem = null;

                Separator.BorderThickness = new Thickness(0);
                Separator.Visibility = Visibility.Collapsed;

                Root?.SetPaneToggleButtonVisibility(MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage) ? Visibility.Visible : Visibility.Collapsed);
                SetTitleBarVisibility(Visibility.Visible);
                Header.Visibility = Visibility.Visible;
                StatusLabel.Visibility = Visibility.Visible;
            }
            else
            {
                ChatsList.SelectionMode = ListViewSelectionMode.Single;
                ChatsList.SelectedItem = _lastSelected;

                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
                Separator.Visibility = Visibility.Visible;

                Root?.SetPaneToggleButtonVisibility(Visibility.Visible);
                SetTitleBarVisibility(MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage) ? Visibility.Collapsed : Visibility.Visible);
                Header.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
                StatusLabel.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
            }

            ChatsList.UpdateViewState(MasterDetail.CurrentState);
        }

        private void UpdateListViewsSelectedItem(long chatId)
        {
            //if (peer == null)
            //{
            //    _lastSelected = null;
            //    ChatsList.SelectedItem = null;

            //    return;
            //}

            var dialog = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == chatId);
            if (dialog != null)
            {
                _lastSelected = dialog;
                ChatsList.SelectedItem = dialog;
            }
            else
            {
                _lastSelected = null;
                ChatsList.SelectedItem = null;
            }
        }

        private void SetTitleBarVisibility(Visibility visibility)
        {
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
            Navigate(e.ClickedItem);
        }

        private async void Navigate(object item)
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

            _lastSelected = item;

            if (item is TLCallGroup callGroup)
            {
                item = callGroup.Message;
            }

            if (item is Message message)
            {
                MasterDetail.NavigationService.NavigateToChat(message.ChatId, message: message.Id);
            }
            else
            {
                SearchField.Text = string.Empty;
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
                MasterDetail.NavigationService.NavigateToChat(chat);
                MasterDetail.NavigationService.GoBackAt(0, false);
            }
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectedItem != null)
            {
                listView.ScrollIntoView(listView.SelectedItem);
            }
            else
            {
                // Find another solution
                await Task.Delay(500);
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
            }
        }

        private void WorkMode_Click(object sender, RoutedEventArgs e)
        {
            var enabled = ((TLViewModelBase)ViewModel).Settings.IsWorkModeEnabled = WorkMode.IsChecked == true;
            ChatsList.UpdateFilterMode(enabled ? ChatFilterMode.Work : ChatFilterMode.None);
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
                else
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

        private void PivotItem_Loaded(object sender, RoutedEventArgs e)
        {
            var dialogs = ViewModel.Chats;
            var contacts = ViewModel.Contacts;

            try
            {
                Execute.BeginOnThreadPool(() =>
                {
                    //dialogs.LoadFirstSlice();
                    contacts.LoadContacts();
                });
            }
            catch { }
        }

        #region Context menu

        private void Dialog_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            CreateFlyoutItem(ref flyout, DialogPin_Loaded, ViewModel.Chats.ChatPinCommand, chat, chat.IsPinned ? Strings.Resources.UnpinFromTop : Strings.Resources.PinToTop);
            CreateFlyoutItem(ref flyout, DialogNotify_Loaded, ViewModel.Chats.ChatNotifyCommand, chat, chat.NotificationSettings.MuteFor > 0 ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications);
            CreateFlyoutItem(ref flyout, DialogMark_Loaded, ViewModel.Chats.ChatMarkCommand, chat, chat.IsUnread() ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread);
            CreateFlyoutItem(ref flyout, DialogClear_Loaded, ViewModel.Chats.ChatClearCommand, chat, Strings.Resources.ClearHistory);
            CreateFlyoutItem(ref flyout, DialogDelete_Loaded, ViewModel.Chats.ChatDeleteCommand, chat, DialogDelete_Text(chat));
            CreateFlyoutItem(ref flyout, DialogDeleteAndStop_Loaded, ViewModel.Chats.ChatDeleteAndStopCommand, chat, Strings.Resources.DeleteAndStop);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void Call_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var call = element.DataContext as TLCallGroup;

            CreateFlyoutItem(ref flyout, _ => Visibility.Visible, ViewModel.Calls.CallDeleteCommand, call, Strings.Resources.Delete);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<Chat, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as Chat);
            if (value == Visibility.Visible)
            {
                var flyoutItem = new MenuFlyoutItem();
                //flyoutItem.Loaded += (s, args) => flyoutItem.Visibility = visibility(parameter as TLMessageCommonBase);
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                flyout.Items.Add(flyoutItem);
            }
        }

        private Visibility DialogMark_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSavedMessages(chat))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private Visibility DialogPin_Loaded(Chat chat)
        {
            //if (!chat.IsPinned)
            //{
            //    var count = ViewModel.Dialogs.LegacyItems.Where(x => x.IsPinned).Count();
            //    var max = ViewModel.CacheService.Config.PinnedDialogsCountMax;

            //    return count < max ? Visibility.Visible : Visibility.Collapsed;
            //}

            if (ViewModel.CacheService.IsChatSponsored(chat))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private Visibility DialogNotify_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSavedMessages(chat))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private Visibility DialogClear_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSponsored(chat))
            {
                return Visibility.Collapsed;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return super.IsChannel || !string.IsNullOrEmpty(supergroup.Username) ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return Visibility.Visible;
        }

        private Visibility DialogDelete_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSponsored(chat))
            {
                return Visibility.Collapsed;
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

            return Visibility.Visible;
        }

        private string DialogDelete_Text(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.Resources.LeaveChannelMenu : Strings.Resources.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.Resources.DeleteAndExit;
            }

            return Strings.Resources.Delete;
        }

        private Visibility DialogDeleteAndStop_Loaded(Chat chat)
        {
            if (ViewModel.CacheService.IsChatSponsored(chat))
            {
                return Visibility.Collapsed;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = ViewModel.ProtoService.GetUser(privata.UserId);
                if (user != null && user.Type is UserTypeBot)
                {
                    var userFull = ViewModel.ProtoService.GetUserFull(privata.UserId);
                    if (userFull != null)
                    {
                        return userFull.IsBlocked ? Visibility.Collapsed : Visibility.Visible;
                    }
                    else
                    {
                        return Visibility.Visible;
                    }
                }
            }

            //var user = dialog.With as TLUser;
            //if (user != null)
            //{
            //    var full = ViewModel.CacheService.GetFullUser(user.Id);
            //    if (full != null)
            //    {
            //        return user.IsBot && !full.IsBlocked ? Visibility.Visible : Visibility.Collapsed;
            //    }
            //    else
            //    {
            //        return user.IsBot ? Visibility.Visible : Visibility.Collapsed;
            //    }

            //    // TODO: 06/05/2017
            //    //element.Visibility = user.IsBot && !user.IsBlocked ? Visibility.Visible : Visibility.Collapsed;
            //}

            return Visibility.Collapsed;
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

        #endregion

        private void NewContact_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(UserCreatePage));
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MasterDetail.AllowCompact = rpMasterTitlebar.SelectedIndex == 0;

            Root?.SetSelectedIndex(rpMasterTitlebar.SelectedIndex);

            SearchField.Visibility = Visibility.Collapsed;
            SettingsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            ChatsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            ContactsOptions.Visibility = rpMasterTitlebar.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

            SearchField.Text = string.Empty;
            SearchField.Visibility = Visibility.Collapsed;

            DialogsPanel.Visibility = Visibility.Visible;
            MainHeader.Visibility = Visibility.Visible;

            try
            {
                rpMasterTitlebar.IsLocked = false;

                ViewModel.Chats.TopChats = null;
                ViewModel.Chats.Search = null;
                ViewModel.Contacts.Search = null;
            }
            catch { }

            if (rpMasterTitlebar.SelectedIndex > 0)
            {
                MasterDetail.Push(true);

                if (Window.Current.Bounds.Width >= 501 && Window.Current.Bounds.Width < 820)
                {
                    while (MasterDetail.NavigationService.Frame.CanGoBack)
                    {
                        MasterDetail.NavigationService.Frame.GoBack();
                    }
                }
            }
        }

        #region Search

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;

            SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_GotFocus(object sender, RoutedEventArgs e)
        {
            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                MainHeader.Visibility = Visibility.Visible;
                SearchField.Visibility = Visibility.Collapsed;

                rpMasterTitlebar.Focus(FocusState.Programmatic);
            }

            Search_TextChanged(null, null);
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrEmpty(SearchField.Text))
            {
                rpMasterTitlebar.IsLocked = false;
                MasterDetail.AllowCompact = true;

                if (rpMasterTitlebar.SelectedIndex == 0)
                {
                    DialogsPanel.Visibility = Visibility.Visible;

                    ViewModel.Chats.TopChats = null;
                    ViewModel.Chats.Search = null;
                }
                else
                {
                    ContactsPanel.Visibility = Visibility.Visible;

                    ViewModel.Contacts.Search = null;
                }
            }
            else if (SearchField.FocusState != FocusState.Unfocused)
            {
                rpMasterTitlebar.IsLocked = true;
                MasterDetail.AllowCompact = false;

                if (rpMasterTitlebar.SelectedIndex == 0)
                {
                    DialogsPanel.Visibility = Visibility.Collapsed;

                    if (string.IsNullOrEmpty(SearchField.Text))
                    {
                        var top = ViewModel.Chats.TopChats = new TopChatsCollection(ViewModel.ProtoService, new TopChatCategoryUsers(), 30);
                        await top.LoadMoreItemsAsync(0);
                    }
                    else
                    {
                        ViewModel.Chats.TopChats = null;
                    }

                    var items = ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text);
                    await items.LoadMoreItemsAsync(0);
                    await items.LoadMoreItemsAsync(1);
                }
                else
                {
                    ContactsPanel.Visibility = Visibility.Collapsed;

                    var items = ViewModel.Contacts.Search = new SearchUsersCollection(ViewModel.ProtoService, SearchField.Text);
                    await items.LoadMoreItemsAsync(0);
                }
            }
        }

        private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
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
            Lock.IsChecked = !Lock.IsChecked;

            if (Lock.IsChecked == true)
            {
                ViewModel.Passcode.Lock();

                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    App.ShowPasscode();
                }
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
                    if (result.User != null || result.Chat != null && result.Chat.Type is ChatTypePrivate privata)
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

                    if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "TextHighlighters"))
                    {
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
                }
                else if (args.Phase == 2)
                {
                    var photo = content.Children[0] as ProfilePicture;
                    if (result.Chat != null)
                    {
                        photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, result.Chat, 36, 36);
                    }
                    else if (result.User != null)
                    {
                        photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, result.User, 36, 36);
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

                content.UpdateMessage(ViewModel.ProtoService, message);
            }

            args.Handled = true;
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
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(UsersListView_ContainerContentChanging);
            }

            args.Handled = true;
        }

        private void DropShadow_Loaded(object sender, RoutedEventArgs e)
        {
            var dropShadow = sender as Border;

            var separator = ElementCompositionPreview.GetElementVisual(dropShadow);
            var shadow = separator.Compositor.CreateDropShadow();
            shadow.BlurRadius = 20;
            shadow.Opacity = 0.25f;
            shadow.Color = Colors.Black;

            var visual = separator.Compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2((float)dropShadow.ActualWidth, (float)dropShadow.ActualHeight);
            visual.Offset = new Vector3(0, 0, 0);
            //visual.Clip = visual.Compositor.CreateInsetClip(-100, 0, 19, 0);

            ElementCompositionPreview.SetElementChildVisual(dropShadow, visual);

            dropShadow.SizeChanged += (s, args) =>
            {
                visual.Size = args.NewSize.ToVector2();
            };
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

                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "TextHighlighters"))
                {
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
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36, 36);
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

            var grid = content.Children[0] as Grid;

            var photo = grid.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.ProtoService.GetMyId())
            {
                photo.Source = PlaceholderHelper.GetChat(null, chat, 48, 48);
                title.Text = Strings.Resources.SavedMessages;
            }
            else
            {
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 48, 48);
                title.Text = ViewModel.ProtoService.GetTitle(chat, true);
            }

            var badge = grid.Children[1] as Border;
            var text = badge.Child as TextBlock;

            badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            text.Text = chat.UnreadCount.ToString();
        }

        public async void NavigationView_ItemClick(RootDestination destination)
        {
            if (destination == RootDestination.NewChat)
            {
                MasterDetail.NavigationService.Navigate(typeof(ChatCreateStep1Page));
            }
            else if (destination == RootDestination.NewSecretChat)
            {
                MasterDetail.NavigationService.Navigate(typeof(SecretChatCreatePage));
            }
            else if (destination == RootDestination.NewChannel)
            {
                MasterDetail.NavigationService.Navigate(typeof(ChannelCreateStep1Page));
            }
            else if (destination == RootDestination.Chats)
            {
                rpMasterTitlebar.SelectedIndex = 0;
            }
            else if (destination == RootDestination.Contacts)
            {
                rpMasterTitlebar.SelectedIndex = 1;
            }
            else if (destination == RootDestination.Calls)
            {
                rpMasterTitlebar.SelectedIndex = 2;
            }
            else if (destination == RootDestination.Settings)
            {
                rpMasterTitlebar.SelectedIndex = 3;
            }
            else if (destination == RootDestination.InviteFriends)
            {
                MasterDetail.NavigationService.Navigate(typeof(InvitePage));
            }
            else if (destination == RootDestination.SavedMessages)
            {
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(ViewModel.ProtoService.GetMyId(), false));
                if (response is Chat chat)
                {
                    MasterDetail.NavigationService.NavigateToChat(chat);
                }
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ProtoService, MasterDetail.NavigationService, "unigram", null, null, null);
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
                presenter.Update(ViewModel.SessionId, ((TLViewModelBase)ViewModel).Settings, ViewModel.Aggregator);
            }
        }
    }

    public class AnyCollection : MvxObservableCollection<object>
    {

    }

    public class NavigationViewTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SessionTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case ISessionService session:
                    return SessionTemplate;
            }

            return null;
            //return base.SelectTemplateCore(item, container);
        }
    }

    public class NavigationViewStyleSelector : StyleSelector
    {
        public Style UserStyle { get; set; }
        public Style ItemContainerStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case MainViewModel user:
                    return UserStyle;
            }

            return ItemContainerStyle;
        }
    }
}
