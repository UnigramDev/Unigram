//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Folders;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    #region Options

    public enum ChooseChatsMode
    {
        Chats,
        Contacts
    }

    public record ChooseChatsOptions
    {
        public bool AllowAll => AllowChannelChats && AllowGroupChats && AllowBotChats && AllowUserChats && AllowSecretChats && AllowSelf && !CanPostMessages && !CanInviteUsers && !CanShareContact;

        public bool AllowChannelChats { get; set; } = true;
        public bool AllowGroupChats { get; set; } = true;
        public bool AllowBotChats { get; set; } = true;
        public bool AllowUserChats { get; set; } = true;
        public bool AllowSecretChats { get; set; } = true;

        public bool AllowSelf { get; set; } = true;

        public bool CanPostMessages { get; set; } = false;
        public bool CanInviteUsers { get; set; } = false;
        public bool CanShareContact { get; set; } = false;

        public ChooseChatsMode Mode { get; set; } = ChooseChatsMode.Chats;

        public bool ShowMessages { get; set; } = false;

        #region Predefined

        public static readonly ChooseChatsOptions All = new()
        {
            AllowChannelChats = true,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = true,
            AllowSelf = true,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Chats,
            ShowMessages = true
        };

        public static readonly ChooseChatsOptions GroupsAndChannels = new()
        {
            AllowChannelChats = true,
            AllowGroupChats = true,
            AllowBotChats = false,
            AllowUserChats = false,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = true,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Chats,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions Contacts = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = false,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Contacts,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions ContactsOnly = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = false,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = true,
            Mode = ChooseChatsMode.Contacts,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions Users = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Chats,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions PostMessages = new()
        {
            AllowChannelChats = true,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = true,
            AllowSelf = true,
            CanPostMessages = true,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Chats,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions InviteUsers = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = true,
            CanShareContact = false,
            Mode = ChooseChatsMode.Chats,
            ShowMessages = false
        };

        public static readonly ChooseChatsOptions Privacy = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            Mode = ChooseChatsMode.Contacts,
            ShowMessages = false
        };

        #endregion

        public virtual bool Allow(IClientService clientService, Chat chat)
        {
            if (AllowAll)
            {
                return true;
            }

            switch (chat.Type)
            {
                case ChatTypeBasicGroup:
                    if (AllowGroupChats)
                    {
                        if (CanPostMessages)
                        {
                            return clientService.CanPostMessages(chat);
                        }
                        else if (CanInviteUsers)
                        {
                            return clientService.CanInviteUsers(chat);
                        }

                        return true;
                    }
                    return false;
                case ChatTypePrivate privata:
                    if (privata.UserId == clientService.Options.MyId)
                    {
                        return AllowSelf;
                    }
                    else if (clientService.TryGetUser(privata.UserId, out User user))
                    {
                        if (user.Type is UserTypeBot)
                        {
                            return AllowBotChats && !CanShareContact;
                        }
                        else if (CanShareContact)
                        {
                            return user.PhoneNumber.Length > 0;
                        }
                    }
                    return AllowUserChats;
                case ChatTypeSecret:
                    return AllowSecretChats;
                case ChatTypeSupergroup supergroup:
                    if (supergroup.IsChannel ? AllowChannelChats : AllowGroupChats)
                    {
                        if (CanPostMessages)
                        {
                            return clientService.CanPostMessages(chat);
                        }
                        else if (CanInviteUsers)
                        {
                            return clientService.CanInviteUsers(chat);
                        }

                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        public virtual bool Allow(IClientService clientService, User user)
        {
            if (AllowAll)
            {
                return true;
            }

            if (user.Id == clientService.Options.MyId)
            {
                return AllowSelf;
            }
            else if (user.Type is UserTypeBot)
            {
                return AllowBotChats && !CanShareContact;
            }
            else if (CanShareContact)
            {
                return user.PhoneNumber.Length > 0;
            }

            return AllowUserChats;
        }
    }

    public record ChooseChatsOptionsRequestUsers : ChooseChatsOptions
    {
        public ChooseChatsOptionsRequestUsers(ChooseChatsConfigurationRequestUsers requestUsers)
        {
            UserIsPremium = requestUsers.UserIsPremium;
            RestrictUserIsPremium = requestUsers.RestrictUserIsPremium;
            UserIsBot = requestUsers.UserIsBot;
            RestrictUserIsBot = requestUsers.RestrictUserIsBot;

            Mode = ChooseChatsMode.Contacts;
        }

        /// <summary>
        /// True, if the shared users must be Telegram Premium users; otherwise, the shared
        /// users must not be Telegram Premium users. Ignored if RestrictUserIsPremium is
        /// false.
        /// </summary>
        public bool UserIsPremium { get; set; }

        /// <summary>
        /// True, if the shared users must or must not be Telegram Premium users.
        /// </summary>
        public bool RestrictUserIsPremium { get; set; }

        /// <summary>
        /// True, if the shared users must be bots; otherwise, the shared users must not
        /// be bots. Ignored if RestrictUserIsBot is false.
        /// </summary>
        public bool UserIsBot { get; set; }

        /// <summary>
        /// True, if the shared users must or must not be bots.
        /// </summary>
        public bool RestrictUserIsBot { get; set; }

        public override bool Allow(IClientService clientService, Chat chat)
        {
            if (clientService.TryGetUser(chat, out User user))
            {
                return Allow(clientService, user);
            }

            return false;
        }

        public override bool Allow(IClientService clientService, User user)
        {
            if (RestrictUserIsBot)
            {
                return UserIsBot == user.Type is UserTypeBot;
            }

            if (RestrictUserIsPremium)
            {
                return UserIsPremium == user.IsPremium;
            }

            return user.Type is UserTypeBot or UserTypeRegular;
        }
    }

    public record ChooseChatsOptionsRequestChat : ChooseChatsOptions
    {
        public ChooseChatsOptionsRequestChat(ChooseChatsConfigurationRequestChat requestChat)
        {
            BotIsMember = requestChat.BotIsMember;
            BotAdministratorRights = requestChat.BotAdministratorRights;
            UserAdministratorRights = requestChat.UserAdministratorRights;
            ChatIsCreated = requestChat.ChatIsCreated;
            ChatHasUsername = requestChat.ChatHasUsername;
            RestrictChatHasUsername = requestChat.RestrictChatHasUsername;
            ChatIsForum = requestChat.ChatIsForum;
            RestrictChatIsForum = requestChat.RestrictChatIsForum;
            ChatIsChannel = requestChat.ChatIsChannel;

            Mode = ChooseChatsMode.Chats;
        }

        /// <summary>
        /// True, if the bot must be a member of the chat; for basic group and supergroup
        /// chats only.
        /// </summary>
        public bool BotIsMember { get; }

        /// <summary>
        /// Expected bot administrator rights in the chat; may be null if they aren't restricted.
        /// </summary>
        public ChatAdministratorRights BotAdministratorRights { get; }

        /// <summary>
        /// Expected user administrator rights in the chat; may be null if they aren't restricted.
        /// </summary>
        public ChatAdministratorRights UserAdministratorRights { get; }

        /// <summary>
        /// True, if the chat must be created by the current user.
        /// </summary>
        public bool ChatIsCreated { get; }

        /// <summary>
        /// True, if the chat must have a username; otherwise, the chat must not have a username.
        /// Ignored if RestrictChatHasUsername is false.
        /// </summary>
        public bool ChatHasUsername { get; }

        /// <summary>
        /// True, if the chat must or must not have a username.
        /// </summary>
        public bool RestrictChatHasUsername { get; }

        /// <summary>
        /// True, if the chat must be a forum supergroup; otherwise, the chat must not be
        /// a forum supergroup. Ignored if RestrictChatIsForum is false.
        /// </summary>
        public bool ChatIsForum { get; }

        /// <summary>
        /// True, if the chat must or must not be a forum supergroup.
        /// </summary>
        public bool RestrictChatIsForum { get; }

        /// <summary>
        /// True, if the chat must be a channel; otherwise, a basic group or a supergroup
        /// chat is shared.
        /// </summary>
        public bool ChatIsChannel { get; }

        public override bool Allow(IClientService clientService, Chat chat)
        {
            if (ChatIsCreated)
            {
                return false;
            }

            if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (RestrictChatHasUsername && ChatHasUsername != supergroup.HasActiveUsername())
                {
                    return false;
                }
                else if (RestrictChatIsForum && ChatIsForum != supergroup.IsForum)
                {
                    return false;
                }

                return ChatIsChannel == supergroup.IsChannel;
            }
            else if (clientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                if (RestrictChatHasUsername && ChatHasUsername)
                {
                    return false;
                }
                else if (RestrictChatIsForum && ChatIsForum)
                {
                    return false;
                }

                return !ChatIsChannel;
            }

            return false;
        }

        public override bool Allow(IClientService clientService, User user)
        {
            return false;
        }
    }

    #endregion

    #region Configurations

    public partial class ChooseChatsConfigurationGroupCall : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationGroupCall(int groupCallId)
        {
            GroupCallId = groupCallId;
        }

        public int GroupCallId { get; }
    }

    public partial class ChooseChatsConfigurationDataPackage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationDataPackage(DataPackageView package)
        {
            Package = package;
        }

        public DataPackageView Package { get; }
    }

    public partial class ChooseChatsConfigurationSwitchInline : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationSwitchInline(string query, TargetChat targetChat, User bot)
        {
            Query = query;
            TargetChat = targetChat;
            Bot = bot;
        }

        public string Query { get; }

        public TargetChat TargetChat { get; }

        public User Bot { get; }
    }

    public partial class ChooseChatsConfigurationPostText : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostText(FormattedText text)
        {
            Text = text;
        }

        public ChooseChatsConfigurationPostText(string text)
        {
            Text = new FormattedText(text, Array.Empty<TextEntity>());
        }

        public FormattedText Text { get; }
    }

    public partial class ChooseChatsConfigurationShareMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareMessage(long chatId, long messageId, bool withMyScore = false)
        {
            ChatId = chatId;
            MessageId = messageId;
            WithMyScore = withMyScore;
        }

        public long ChatId { get; }

        public long MessageId { get; }

        public bool WithMyScore { get; }
    }

    public partial class ChooseChatsConfigurationReplyToMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationReplyToMessage(MessageViewModel message, InputTextQuote quote = null)
        {
            Message = message.Get();
            Quote = quote;
        }

        public Message Message { get; }

        public InputTextQuote Quote { get; }
    }

    public partial class ChooseChatsConfigurationShareStory : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareStory(long chatId, int storyId)
        {
            ChatId = chatId;
            StoryId = storyId;
        }

        public long ChatId { get; }

        public int StoryId { get; }
    }

    public partial class ChooseChatsConfigurationShareMessages : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareMessages(IEnumerable<MessageId> messageIds)
        {
            MessageIds = messageIds.ToArray();
        }

        public IList<MessageId> MessageIds { get; }
    }

    public partial class ChooseChatsConfigurationPostLink : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostLink(HttpUrl url)
        {
            Url = url;
        }

        public ChooseChatsConfigurationPostLink(InternalLinkType internalLink)
        {
            InternalLink = internalLink;
        }

        public HttpUrl Url { get; }

        public InternalLinkType InternalLink { get; }
    }

    public partial class ChooseChatsConfigurationPostMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostMessage(InputMessageContent content)
        {
            Content = content;
        }

        public InputMessageContent Content { get; }
    }

    public partial class ChooseChatsConfigurationStartBot : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationStartBot(User bot, string token = null)
        {
            Bot = bot;
            Token = token;
        }

        public User Bot { get; }

        public string Token { get; }
    }

    public partial class ChooseChatsConfigurationRequestUsers : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationRequestUsers(long chatId, long messageId, KeyboardButtonTypeRequestUsers requestUsers)
        {
            ChatId = chatId;
            MessageId = messageId;

            MaxQuantity = requestUsers.MaxQuantity;
            UserIsPremium = requestUsers.UserIsPremium;
            RestrictUserIsPremium = requestUsers.RestrictUserIsPremium;
            UserIsBot = requestUsers.UserIsBot;
            RestrictUserIsBot = requestUsers.RestrictUserIsBot;
            Id = requestUsers.Id;
        }

        public long ChatId { get; }

        public long MessageId { get; }

        /// <summary>
        /// The maximum number of users to share.
        /// </summary>
        public int MaxQuantity { get; }

        /// <summary>
        /// True, if the shared users must be Telegram Premium users; otherwise, the shared
        /// users must not be Telegram Premium users. Ignored if RestrictUserIsPremium is
        /// false.
        /// </summary>
        public bool UserIsPremium { get; }

        /// <summary>
        /// True, if the shared users must or must not be Telegram Premium users.
        /// </summary>
        public bool RestrictUserIsPremium { get; }

        /// <summary>
        /// True, if the shared users must be bots; otherwise, the shared users must not
        /// be bots. Ignored if RestrictUserIsBot is false.
        /// </summary>
        public bool UserIsBot { get; }

        /// <summary>
        /// True, if the shared users must or must not be bots.
        /// </summary>
        public bool RestrictUserIsBot { get; }

        /// <summary>
        /// Unique button identifier.
        /// </summary>
        public int Id { get; }
    }

    public partial class ChooseChatsConfigurationRequestChat : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationRequestChat(KeyboardButtonTypeRequestChat requestChat)
        {
            BotIsMember = requestChat.BotIsMember;
            BotAdministratorRights = requestChat.BotAdministratorRights;
            UserAdministratorRights = requestChat.UserAdministratorRights;
            ChatIsCreated = requestChat.ChatIsCreated;
            ChatHasUsername = requestChat.ChatHasUsername;
            RestrictChatHasUsername = requestChat.RestrictChatHasUsername;
            ChatIsForum = requestChat.ChatIsForum;
            RestrictChatIsForum = requestChat.RestrictChatIsForum;
            ChatIsChannel = requestChat.ChatIsChannel;
            Id = requestChat.Id;
        }

        /// <summary>
        /// True, if the bot must be a member of the chat; for basic group and supergroup
        /// chats only.
        /// </summary>
        public bool BotIsMember { get; }

        /// <summary>
        /// Expected bot administrator rights in the chat; may be null if they aren't restricted.
        /// </summary>
        public ChatAdministratorRights BotAdministratorRights { get; }

        /// <summary>
        /// Expected user administrator rights in the chat; may be null if they aren't restricted.
        /// </summary>
        public ChatAdministratorRights UserAdministratorRights { get; }

        /// <summary>
        /// True, if the chat must be created by the current user.
        /// </summary>
        public bool ChatIsCreated { get; }

        /// <summary>
        /// True, if the chat must have a username; otherwise, the chat must not have a username.
        /// Ignored if RestrictChatHasUsername is false.
        /// </summary>
        public bool ChatHasUsername { get; }

        /// <summary>
        /// True, if the chat must or must not have a username.
        /// </summary>
        public bool RestrictChatHasUsername { get; }

        /// <summary>
        /// True, if the chat must be a forum supergroup; otherwise, the chat must not be
        /// a forum supergroup. Ignored if RestrictChatIsForum is false.
        /// </summary>
        public bool ChatIsForum { get; }

        /// <summary>
        /// True, if the chat must or must not be a forum supergroup.
        /// </summary>
        public bool RestrictChatIsForum { get; }

        /// <summary>
        /// True, if the chat must be a channel; otherwise, a basic group or a supergroup
        /// chat is shared.
        /// </summary>
        public bool ChatIsChannel { get; }

        /// <summary>
        /// Unique button identifier.
        /// </summary>
        public int Id { get; }
    }

    public abstract class ChooseChatsConfiguration
    {

    }

    #endregion

    public sealed partial class ChooseChatsPopup : ContentPopup
    {
        public ChooseChatsViewModel ViewModel => DataContext as ChooseChatsViewModel;

        public ChooseChatsPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Close;

            CaptionInput.CustomEmoji = CustomEmoji;
        }

        [Obsolete]
        public void Legacy(int sessionId)
        {
            DataContext = TypeResolver.Current.Resolve<ChooseChatsViewModel>(sessionId);
        }

        private bool _legacyNavigated;

        public override void OnNavigatedTo(object parameter)
        {
            if (_legacyNavigated)
            {
                return;
            }

            _legacyNavigated = true;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(ViewModel.SessionId);
            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.Options.Mode == ChooseChatsMode.Contacts)
            {
                ChatFolders.Visibility = Visibility.Collapsed;
            }

            if (ViewModel.IsCommentEnabled)
            {
                CommentPanel.Visibility = Visibility.Visible;
                Scrim.BottomInset = 0;

                PrimaryButtonText = string.Empty;
                SecondaryButtonText = string.Empty;
                IsDismissButtonVisible = true;
            }
            else
            {
                CommentPanel.Visibility = Visibility.Collapsed;
                Scrim.BottomInset = 32;

                PrimaryButtonText = ViewModel.SelectionMode != ListViewSelectionMode.None
                    ? ViewModel.PrimaryButtonText
                    : string.Empty;
                SecondaryButtonText = Strings.Cancel;
                IsDismissButtonVisible = false;
            }
        }

        protected override void OnApplyTemplate()
        {
            if (ViewModel != null)
            {
                OnNavigatedTo(null);
            }

            base.OnApplyTemplate();
        }

        private void Send_ContextRequested(object sender, ContextRequestedEventArgs args)
        {
            if (ViewModel.IsSendAsCopyEnabled)
            {
                var flyout = new MenuFlyout();
                flyout.CreateFlyoutItem(() => { ViewModel.SendAsCopy = true; Hide(ContentDialogResult.Primary); }, Strings.HideSenderNames, Icons.DocumentCopy);
                flyout.CreateFlyoutItem(() => { ViewModel.RemoveCaptions = true; Hide(ContentDialogResult.Primary); }, Strings.HideCaption, Icons.Block);

                flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.TopEdgeAlignedRight);
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "PreSelectedItems", StringComparison.OrdinalIgnoreCase))
            {
                ChatsPanel.SelectedItems.Clear();
                ChatsPanel.SelectedItems.AddRange(ViewModel.SelectedItems);
            }
        }

        public object Header
        {
            get => ChatsPanel.Header;
            set => ChatsPanel.Header = value;
        }

        #region Show

        public static async Task<Chat> PickChatAsync(INavigationService navigationService, string title, ChooseChatsOptions options)
        {
            var popup = new ChooseChatsPopup();
            popup.Legacy(navigationService.SessionId);
            popup.ViewModel.NavigationService = navigationService;
            popup.ViewModel.Title = title;
            popup.ChatFolders.Visibility = Visibility.Collapsed;

            var confirm = await popup.PickAsync(navigationService.XamlRoot, Array.Empty<long>(), options, ListViewSelectionMode.Single);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return popup.ViewModel.SelectedItems.FirstOrDefault();
        }

        public static async Task<User> PickUserAsync(IClientService clientService, INavigationService navigationService, string title, bool contact)
        {
            return clientService.GetUser(await PickChatAsync(navigationService, title, contact ? ChooseChatsOptions.Contacts : new ChooseChatsOptions()
            {
                AllowChannelChats = false,
                AllowGroupChats = false,
                AllowBotChats = false,
                AllowUserChats = true,
                AllowSecretChats = false,
                AllowSelf = false,
                CanPostMessages = false,
                CanInviteUsers = false,
                CanShareContact = false,
                Mode = ChooseChatsMode.Chats,
                ShowMessages = false
            }));
        }

        public static async Task<IList<Chat>> PickChatsAsync(INavigationService navigationService, string title, long[] selected, ChooseChatsOptions options, ListViewSelectionMode selectionMode = ListViewSelectionMode.Multiple, bool allowEmptySelection = false)
        {
            var popup = new ChooseChatsPopup();
            popup.Legacy(navigationService.SessionId);
            popup.ViewModel.NavigationService = navigationService;
            popup.ViewModel.SelectionMode = selectionMode;
            popup.ViewModel.AllowEmptySelection = allowEmptySelection;
            popup.ViewModel.Title = title;
            popup.PrimaryButtonText = Strings.OK;

            var confirm = await popup.PickAsync(navigationService.XamlRoot, selected, options);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return popup.ViewModel.SelectedItems.ToList();
        }

        public static async Task<IList<User>> PickUsersAsync(IClientService clientService, INavigationService navigationService, string title, ListViewSelectionMode selectionMode = ListViewSelectionMode.Multiple, bool allowEmptySelection = false)
        {
            return (await PickChatsAsync(navigationService, title, Array.Empty<long>(), ChooseChatsOptions.InviteUsers, selectionMode, allowEmptySelection))?.Select(x => clientService.GetUser(x)).Where(x => x != null).ToList();
        }

        public Task<ContentDialogResult> PickAsync(XamlRoot xamlRoot, IList<long> selectedItems, ChooseChatsOptions options, ListViewSelectionMode selectionMode = ListViewSelectionMode.Multiple)
        {
            ViewModel.SelectionMode = selectionMode;
            ViewModel.Options = options;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = true;

            ViewModel.PreSelectedItems = selectedItems;

            return ShowAsync(xamlRoot);
        }

        private Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)
        {
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.NavigatedToAsync(null, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync(xamlRoot);
        }

        #endregion

        #region PickFiltersAsync

        public static async Task<IList<ChatFolderElement>> AddExecute(INavigationService navigationService, bool include, bool allowFilters, bool business, IList<ChatFolderElement> target)
        {
            if (allowFilters)
            {
                //var target = new List<ChatFolderElement>();

                var flags = new List<FolderFlag>();

                if (business)
                {
                    if (include)
                    {
                        flags.Add(new FolderFlag(ChatListFolderFlags.NewChats));
                        flags.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
                        flags.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));
                    }
                    else
                    {
                        flags.Add(new FolderFlag(ChatListFolderFlags.ExistingChats));
                        flags.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
                        flags.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));
                    }
                }
                else if (include)
                {
                    flags.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
                    flags.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));
                    flags.Add(new FolderFlag(ChatListFolderFlags.IncludeGroups));
                    flags.Add(new FolderFlag(ChatListFolderFlags.IncludeChannels));
                    flags.Add(new FolderFlag(ChatListFolderFlags.IncludeBots));
                }
                else
                {
                    flags.Add(new FolderFlag(ChatListFolderFlags.ExcludeMuted));
                    flags.Add(new FolderFlag(ChatListFolderFlags.ExcludeRead));
                    flags.Add(new FolderFlag(ChatListFolderFlags.ExcludeArchived));
                }

                var header = new MultipleListView();
                header.SelectionMode = ListViewSelectionMode.Multiple;
                header.ItemsSource = flags;
                header.ItemTemplate = BootStrapper.Current.Resources["FolderPickerTemplate"] as DataTemplate;
                header.ContainerContentChanging += Header_ContainerContentChanging;
                header.Padding = new Thickness(12, 0, 12, 0);
                header.ItemContainerTransitions = new Windows.UI.Xaml.Media.Animation.TransitionCollection();

                foreach (var folder in target.OfType<FolderFlag>())
                {
                    var already = flags.FirstOrDefault(x => x.Flag == folder.Flag);
                    if (already != null)
                    {
                        header.SelectedItems.Add(already);
                    }
                }

                var panel = new StackPanel();
                panel.Children.Add(new Border
                {
                    Child = new TextBlock
                    {
                        Text = Strings.FilterChatTypes,
                        Padding = new Thickness(24, 8, 0, 4),
                        Style = BootStrapper.Current.Resources["BaseTextBlockStyle"] as Style
                    }
                });
                panel.Children.Add(header);
                panel.Children.Add(new Border
                {
                    Child = new TextBlock
                    {
                        Text = Strings.FilterChats,
                        Padding = new Thickness(24, 16, 0, 4),
                        Style = BootStrapper.Current.Resources["BaseTextBlockStyle"] as Style
                    }
                });

                var popup = new ChooseChatsPopup();
                popup.Legacy(navigationService.SessionId);
                popup.ViewModel.NavigationService = navigationService;
                popup.ViewModel.Title = include ? Strings.FilterAlwaysShow : Strings.FilterNeverShow;
                popup.ViewModel.AllowEmptySelection = true;
                popup.ViewModel.Folders.Clear();
                popup.Header = panel;
                popup.IsPrimaryButtonEnabled = true;

                var confirm = await popup.PickAsync(navigationService.XamlRoot, target.OfType<FolderChat>().Select(x => x.ChatId).ToArray(), ChooseChatsOptions.All);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                target.Clear();

                foreach (var folder in header.SelectedItems.OfType<FolderFlag>())
                {
                    target.Add(folder);
                }

                foreach (var chat in popup.ViewModel.SelectedItems)
                {
                    if (chat == null)
                    {
                        continue;
                    }

                    target.Add(new FolderChat(chat.Id));
                }

                return target;
            }
            else
            {
                var popup = new ChooseChatsPopup();
                popup.Legacy(navigationService.SessionId);
                popup.ViewModel.NavigationService = navigationService;
                popup.ViewModel.Title = include ? Strings.FilterAlwaysShow : Strings.FilterNeverShow;
                popup.ViewModel.AllowEmptySelection = true;
                popup.ViewModel.Folders.Clear();
                popup.IsPrimaryButtonEnabled = true;

                var confirm = await popup.PickAsync(navigationService.XamlRoot, target.OfType<FolderChat>().Select(x => x.ChatId).ToArray(), ChooseChatsOptions.All);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                target.Clear();

                foreach (var chat in popup.ViewModel.SelectedItems)
                {
                    if (chat == null)
                    {
                        continue;
                    }

                    target.Add(new FolderChat(chat.Id));
                }

                return target;
            }
        }

        private static void Header_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell content && args.Item is FolderFlag folder)
            {
                content.UpdateState(args.ItemContainer.IsSelected, false, true);
                content.UpdateChatFolder(folder);

                args.Handled = true;
            }
        }

        #endregion

        #region Recycle

        private bool _focused = true;

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            if (_focused)
            {
                _focused = false;
                args.ItemContainer.Loaded += ItemContainer_Loaded;

            }

            args.IsContainerPrepared = true;
        }

        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is SelectorItem container)
            {
                container.Loaded -= ItemContainer_Loaded;
                container.Focus(FocusState.Pointer);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell chatCell)
            {
                chatCell.UpdateState(args.ItemContainer.IsSelected, false, true);
                chatCell.UpdateChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
            else if (args.ItemContainer.ContentTemplateRoot is ForumTopicShareCell topicCell)
            {
                topicCell.UpdateCell(ViewModel.ClientService, args.Item as ForumTopic);
                args.Handled = true;
            }
        }

        #endregion

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;

            FindName(nameof(SearchPanel));
            ChatListPanel.Visibility = Visibility.Visible;
            SearchPanel.Visibility = Visibility.Visible;

            SearchClear.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (show)
            {
                SearchField.ControlledList = SearchPanel.Root;
            }

            var chats = ElementComposition.GetElementVisual(ChatListPanel);
            var panel = ElementComposition.GetElementVisual(SearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3(ChatListPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ChatListPanel.Visibility = _searchCollapsed ? Visibility.Visible : Visibility.Collapsed;
                SearchPanel.Visibility = _searchCollapsed ? Visibility.Collapsed : Visibility.Visible;

                if (_searchCollapsed)
                {
                    ChatsPanel.Focus(FocusState.Pointer);
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

        private void Search_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (args.FocusState == FocusState.Programmatic)
            {
                args.TryCancel();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Pointer && _searchCollapsed)
            {
                ShowHideSearch(true);
                ViewModel.SearchChats.Query = SearchField.Text;
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState != FocusState.Unfocused)
            {
                ShowHideSearch(true);
            }

            ViewModel.SearchChats.Query = SearchField.Text;
        }

        private void SearchClear_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ShowHideSearch(false);
            }
            else
            {
                SearchField.Text = string.Empty;
            }
        }

        #endregion

        #region Forum

        private bool _forumCollapsed = true;

        private void ShowHideForum(Chat chat)
        {
            if (chat == null)
            {
                ShowHideForum(false);
                return;
            }

            ShowHideForum(true);

            var viewModel = new TopicListViewModel.ItemsCollection(ViewModel.ClientService, ViewModel.Aggregator, null, chat);
            ForumList.ItemsSource = viewModel;
        }

        private void ShowHideForum(bool show)
        {
            if (_forumCollapsed != show)
            {
                return;
            }

            _forumCollapsed = !show;

            FindName(nameof(ForumGrid));
            MainGrid.Visibility = Visibility.Visible;
            ForumGrid.Visibility = Visibility.Visible;

            var chats = ElementComposition.GetElementVisual(MainGrid);
            var panel = ElementComposition.GetElementVisual(ForumGrid);

            chats.CenterPoint = panel.CenterPoint = new Vector3(MainGrid.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                MainGrid.Visibility = _forumCollapsed ? Visibility.Visible : Visibility.Collapsed;
                ForumGrid.Visibility = _forumCollapsed ? Visibility.Collapsed : Visibility.Visible;

                if (_forumCollapsed)
                {
                    ChatsPanel.Focus(FocusState.Pointer);
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

        #endregion

        #region Comment

        private Visibility ConvertCommentVisibility(int count, bool enabled)
        {
            return count > 0 && enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Binding

        private bool ConvertButtonEnabled(bool allowEmpty, int count)
        {
            if (Send != null)
            {
                return Send.IsEnabled = allowEmpty || count > 0;
            }

            return allowEmpty || count > 0;
        }

        #endregion

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.Configuration is ChooseChatsConfigurationRequestUsers requestUsers && e.AddedItems != null)
            {
                if (ChatsPanel.SelectedItems.Count > requestUsers.MaxQuantity)
                {
                    foreach (var item in e.AddedItems)
                    {
                        ChatsPanel.SelectedItems.Remove(item);
                    }

                    ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.BotMultiContactsSelectorLimit, requestUsers.MaxQuantity), ToastPopupIcon.Info);
                }
            }

            var selection = ChatsPanel.SelectedItems
                .OfType<Chat>()
                .Where(x => ViewModel.ClientService.IsForum(x) ? ViewModel.SelectedTopics.ContainsKey(x.Id) : true);

            ViewModel.SelectedItems = new MvxObservableCollection<Chat>(selection);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick(e.ClickedItem as Chat, true);
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem;
            if (item is SearchResult result)
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

            if (item is User user)
            {
                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                if (response is Chat)
                {
                    item = response as Chat;
                }
            }
            else if (item is ForumTopic topic && ForumList.ItemsSource is TopicListViewModel.ItemsCollection collection)
            {
                item = collection.Chat;
                ViewModel.SelectedTopics[collection.Chat.Id] = topic.Info.MessageThreadId;
                ShowHideForum(null);
            }

            if (ViewModel.SearchChats.CanSendMessageToUser && ViewModel.ClientService.TryGetUser(item as Chat, out User tempUser))
            {
                var response = await ViewModel.ClientService.SendAsync(new CanSendMessageToUser(tempUser.Id, true));
                if (response is CanSendMessageToUserResultUserRestrictsNewChats)
                {
                    var text = string.Format(Strings.MessageLockedPremiumLocked, tempUser.FirstName);
                    var markdown = ClientEx.ParseMarkdown(text);

                    var confirm = await ToastPopup.ShowActionAsync(XamlRoot, markdown, Strings.UserBlockedNonPremiumButton, ToastPopupIcon.Premium);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        Hide();
                        ViewModel.NavigationService.ShowPromo();
                    }

                    return;
                }
            }

            var chat = item as Chat;
            if (chat == null || ItemClick(chat, e.ClickedItem is Chat))
            {
                return;
            }

            chat = ViewModel.Items.FirstOrDefault(x => x.Id == chat.Id) ?? chat;
            SearchField.Text = string.Empty;
            ShowHideSearch(false);

            var items = ViewModel.Items;
            var selectedItems = ViewModel.SelectedItems;

            var index = items.IndexOf(chat);
            if (index >= 0)
            {
                if (index > 0)
                {
                    items.Remove(chat);
                    items.Insert(1, chat);
                }
            }
            else if (items.Count > 0)
            {
                items.Insert(1, chat);
            }
            else
            {
                items.Add(chat);
            }

            if (ChatsPanel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ChatsPanel.SelectedItems.Add(chat);
            }
            else
            {
                ChatsPanel.SelectedItem = chat;
            }
        }

        private bool ItemClick(Chat chat, bool origin)
        {
            if (ViewModel.Options.CanPostMessages && (ViewModel.ClientService.IsSavedMessages(chat) || ViewModel.SelectionMode == ListViewSelectionMode.None))
            {
                if (ViewModel.SelectedItems.Empty())
                {
                    ViewModel.SelectedItems = new MvxObservableCollection<Chat>(new[] { chat });
                    ViewModel.SendCommand.Execute();

                    Hide();
                    return true;
                }
            }
            else if (ViewModel.Options.CanPostMessages && origin && ViewModel.ClientService.IsForum(chat))
            {
                if (ViewModel.SelectedItems.Contains(chat))
                {
                    ViewModel.SelectedTopics.Remove(chat.Id);
                    ShowHideForum(null);
                }
                else
                {
                    ShowHideForum(chat);
                }

                return false;
            }

            return false;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0)
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsControl(character[0]))
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
            {
                if (character == "\u0016" && CaptionInput.CanPasteClipboardContent)
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.PasteFromClipboard();
                }
                else if (character == "\r" && IsPrimaryButtonEnabled && (SearchPanel == null || SearchPanel.Visibility == Visibility.Collapsed))
                {
                    Accept();
                }
                else
                {
                    Search_Click(null, null);

                    SearchField.Focus(FocusState.Keyboard);
                    SearchField.Text = character;
                    SearchField.SelectionStart = character.Length;
                }

                args.Handled = true;
            }
        }

        private void Accept()
        {
            if (CaptionInput.HandwritingView.IsOpen)
            {
                void handler(object s, RoutedEventArgs args)
                {
                    CaptionInput.HandwritingView.Unloaded -= handler;

                    ViewModel.Caption = CaptionInput.GetFormattedText();
                    Hide(ContentDialogResult.Primary);
                }

                CaptionInput.HandwritingView.Unloaded += handler;
                CaptionInput.HandwritingView.TryClose();
            }
            else
            {
                ViewModel.Caption = CaptionInput.GetFormattedText();
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CommentPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Caption = CaptionInput.GetFormattedText();
            ViewModel.SendCommand.Execute();
        }

        private void CaptionInput_Accept(FormattedTextBox sender, EventArgs args)
        {
            Accept();
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Accept();
        }
    }
}
