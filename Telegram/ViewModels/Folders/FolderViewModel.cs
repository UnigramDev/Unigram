//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public class FolderViewModel : ViewModelBase
    {
        public FolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Include = new MvxObservableCollection<ChatFolderElement>();
            Exclude = new MvxObservableCollection<ChatFolderElement>();

            AvailableColors = new ObservableCollection<NameColor>(ClientService.GetAvailableAccentColors()
                .Where(x => x.Id == x.BuiltInAccentColorId)
                .Append(new NameColor(-1)));

            Links = new MvxObservableCollection<ChatFolderInviteLink>();

            Include.CollectionChanged += OnCollectionChanged;
            Exclude.CollectionChanged += OnCollectionChanged;

            SendCommand = new RelayCommand(SendExecute, SendCanExecute);
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SendCommand.RaiseCanExecuteChanged();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ChatFolder folder = null;

            if (parameter is int id)
            {
                var response = await ClientService.SendAsync(new GetChatFolder(id));
                if (response is ChatFolder result)
                {
                    Id = id;
                    Folder = result;
                    folder = result;
                }
                else
                {
                    // TODO
                }
            }
            else
            {
                Id = null;
                Folder = null;
                folder = new ChatFolder();
                folder.PinnedChatIds = new List<long>();
                folder.IncludedChatIds = new List<long>();
                folder.ExcludedChatIds = new List<long>();
            }

            if (folder == null)
            {
                return;
            }

            if (state != null && state.TryGet("included_chat_id", out long includedChatId))
            {
                folder.IncludedChatIds.Add(includedChatId);
            }

            _pinnedChatIds = folder.PinnedChatIds;

            _iconPicked = !string.IsNullOrEmpty(folder.Icon?.Name);
            _originalColorId = folder.ColorId;

            Title = folder.Title;
            Icon = Icons.ParseFolder(folder);
            SelectedColor = IsPremium && folder.ColorId != -1
                ? ClientService.GetAccentColor(folder.ColorId)
                : AvailableColors[^1];

            Links.Clear();

            Include.Clear();
            Exclude.Clear();

            if (folder.IncludeContacts) Include.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeContacts });
            if (folder.IncludeNonContacts) Include.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeNonContacts });
            if (folder.IncludeGroups) Include.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeGroups });
            if (folder.IncludeChannels) Include.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeChannels });
            if (folder.IncludeBots) Include.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeBots });

            if (folder.ExcludeMuted) Exclude.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeMuted });
            if (folder.ExcludeRead) Exclude.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeRead });
            if (folder.ExcludeArchived) Exclude.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeArchived });

            foreach (var chatId in folder.PinnedChatIds.Union(folder.IncludedChatIds))
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Include.Add(new FolderChat { Chat = chat });
            }

            foreach (var chatId in folder.ExcludedChatIds)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Exclude.Add(new FolderChat { Chat = chat });
            }

            UpdateIcon();
            UpdateLinks();
        }

        public int? Id { get; set; }

        private ChatFolder _folder;
        public ChatFolder Folder
        {
            get => _folder;
            set => Set(ref _folder, value);
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _iconPicked;
        private int _originalColorId = -1;

        private ChatFolderIcon2 _icon;
        public ChatFolderIcon2 Icon
        {
            get => _icon;
            private set => Set(ref _icon, value);
        }

        public void SetIcon(ChatFolderIcon2 icon)
        {
            _iconPicked = true;
            Icon = icon;
        }

        private void UpdateIcon()
        {
            if (_iconPicked)
            {
                return;
            }

            Icon = Icons.ParseFolder(GetFolder());
        }

        public ObservableCollection<NameColor> AvailableColors { get; }

        private NameColor _selectedColor;
        public NameColor SelectedColor
        {
            get => _selectedColor;
            set => Set(ref _selectedColor, value);
        }

        private async void UpdateLinks()
        {
            if (Id is int id)
            {
                var response = await ClientService.SendAsync(new GetChatFolderInviteLinks(id));
                if (response is ChatFolderInviteLinks links)
                {
                    Links.ReplaceWith(links.InviteLinks);
                }
            }
        }

        private IList<long> _pinnedChatIds = Array.Empty<long>();

        public MvxObservableCollection<ChatFolderElement> Include { get; private set; }
        public MvxObservableCollection<ChatFolderElement> Exclude { get; private set; }

        public MvxObservableCollection<ChatFolderInviteLink> Links { get; private set; }

        public async void AddIncluded()
        {
            await AddIncludeAsync();
            UpdateIcon();
        }

        public async Task AddIncludeAsync()
        {
            var result = await ChooseChatsPopup.AddExecute(true, _folder == null || (!_folder.IsShareable && Links.Count == 0), false, Include.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FolderChat>())
                {
                    var already = Exclude.OfType<FolderChat>().FirstOrDefault(x => x.Chat.Id == item.Chat.Id);
                    if (already != null)
                    {
                        Exclude.Remove(already);
                    }
                }

                var flags = result.OfType<FolderFlag>().Cast<ChatFolderElement>();
                var chats = result.OfType<FolderChat>().OrderBy(x =>
                {
                    var index = _pinnedChatIds.IndexOf(x.Chat.Id);
                    if (index != -1)
                    {
                        return index;
                    }

                    return int.MaxValue;
                });

                Include.ReplaceWith(flags.Union(chats));
            }
        }

        public async void AddExcluded()
        {
            await AddExcludeAsync();
            UpdateIcon();
        }

        public async Task AddExcludeAsync()
        {
            var result = await ChooseChatsPopup.AddExecute(false, true, false, Exclude.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FolderChat>())
                {
                    var already = Include.OfType<FolderChat>().FirstOrDefault(x => x.Chat.Id == item.Chat.Id);
                    if (already != null)
                    {
                        Include.Remove(already);
                    }
                }

                Exclude.ReplaceWith(result);
            }
        }

        public void RemoveIncluded(ChatFolderElement chat)
        {
            Include.Remove(chat);
            UpdateIcon();
        }

        public void RemoveExcluded(ChatFolderElement chat)
        {
            Exclude.Remove(chat);
            UpdateIcon();
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await SendAsync();
            if (response is ChatFolderInfo)
            {
                NavigationService.GoBack();
            }
        }

        public Task<BaseObject> SendAsync()
        {
            Function function;
            if (Id is int id)
            {
                function = new EditChatFolder(id, GetFolder());
            }
            else
            {
                function = new CreateChatFolder(GetFolder());
            }

            return ClientService.SendAsync(function);
        }

        private bool SendCanExecute()
        {
            return !string.IsNullOrEmpty(Title) && Include.Count > 0;
        }

        private ChatFolder GetFolder()
        {
            var folder = new ChatFolder();
            folder.Title = Title ?? string.Empty;
            folder.Icon = new ChatFolderIcon(_iconPicked ? Enum.GetName(typeof(ChatFolderIcon2), Icon) : string.Empty);
            folder.ColorId = IsPremium ? SelectedColor?.Id ?? -1 : _originalColorId;
            folder.PinnedChatIds = new List<long>();
            folder.IncludedChatIds = new List<long>();
            folder.ExcludedChatIds = new List<long>();

            foreach (var item in Include)
            {
                if (item is FolderFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFolderFlags.IncludeContacts:
                            folder.IncludeContacts = true;
                            break;
                        case ChatListFolderFlags.IncludeNonContacts:
                            folder.IncludeNonContacts = true;
                            break;
                        case ChatListFolderFlags.IncludeGroups:
                            folder.IncludeGroups = true;
                            break;
                        case ChatListFolderFlags.IncludeChannels:
                            folder.IncludeChannels = true;
                            break;
                        case ChatListFolderFlags.IncludeBots:
                            folder.IncludeBots = true;
                            break;
                    }
                }
                else if (item is FolderChat chat)
                {
                    if (_pinnedChatIds.Contains(chat.Chat.Id))
                    {
                        folder.PinnedChatIds.Add(chat.Chat.Id);
                    }
                    else
                    {
                        folder.IncludedChatIds.Add(chat.Chat.Id);
                    }
                }
            }

            foreach (var item in Exclude)
            {
                if (item is FolderFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFolderFlags.ExcludeMuted:
                            folder.ExcludeMuted = true;
                            break;
                        case ChatListFolderFlags.ExcludeRead:
                            folder.ExcludeRead = true;
                            break;
                        case ChatListFolderFlags.ExcludeArchived:
                            folder.ExcludeArchived = true;
                            break;
                    }
                }
                else if (item is FolderChat chat)
                {
                    folder.ExcludedChatIds.Add(chat.Chat.Id);
                }
            }

            return folder;
        }

        public async void OpenLink(ChatFolderInviteLink link)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await ShowPopupAsync(typeof(ShareFolderPopup), Tuple.Create(Id.Value, link), tsc);
            if (confirm == ContentDialogResult.Primary)
            {
                var result = await tsc.Task;
                if (result is IList<long> chats)
                {
                    if (link != null)
                    {
                        await ClientService.SendAsync(new EditChatFolderInviteLink(Id.Value, link.InviteLink, string.Empty, chats));
                    }
                    else
                    {
                        await ClientService.SendAsync(new CreateChatFolderInviteLink(Id.Value, string.Empty, chats));
                    }

                    UpdateLinks();
                }
            }
        }

        public async void CreateLink()
        {
            if (string.IsNullOrEmpty(Title))
            {
                ShowPopup(Strings.FilterInviteErrorEmptyName, Strings.AppName, Strings.OK);
                return;
            }

            if (Exclude.Any())
            {
                ShowPopup(Strings.FilterInviteErrorExcluded, Strings.AppName, Strings.OK);
                return;
            }

            if (Include.Any(x => x is FolderFlag) || Include.Empty())
            {
                ShowPopup(Strings.FilterInviteErrorTypes, Strings.AppName, Strings.OK);
                return;
            }

            if (Id == null)
            {
                // TODO: IMHO folder should be created here
                ShowPopup(Strings.FilterFinishCreating, Strings.AppName, Strings.OK);
                return;
            }

            var shareableItems = new List<long>(Include.OfType<FolderChat>().Select(x => x.Chat.Id));

            foreach (var item in Include.OfType<FolderChat>())
            {
                var chat = item.Chat;
                if (chat.Permissions.CanInviteUsers)
                {
                    continue;
                }
                else if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.CanInviteUsers())
                    {
                        continue;
                    }
                    else if (supergroup.HasActiveUsername() && !supergroup.JoinByRequest)
                    {
                        continue;
                    }
                }
                else if (ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                {
                    if (basicGroup.CanInviteUsers())
                    {
                        continue;
                    }
                }

                shareableItems.Remove(chat.Id);
            }

            if (shareableItems.Count > 0)
            {
                var response = await ClientService.SendAsync(new CreateChatFolderInviteLink(Id.Value, string.Empty, shareableItems));
                if (response is ChatFolderInviteLink link)
                {
                    OpenLink(link);
                }
                else if (response is Error error)
                {
                    if (error.MessageEquals(ErrorType.USER_CHANNELS_TOO_MUCH))
                    {
                        ShowPopup(Strings.FolderLinkOtherAdminLimitError, Strings.AppName, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.CHANNELS_TOO_MUCH))
                    {
                        NavigationService.ShowLimitReached(new PremiumLimitTypeSupergroupCount());
                    }
                    else if (error.MessageEquals(ErrorType.INVITES_TOO_MUCH))
                    {
                        NavigationService.ShowLimitReached(new PremiumLimitTypeChatFolderInviteLinkCount());
                    }
                    else if (error.MessageEquals(ErrorType.CHATLISTS_TOO_MUCH))
                    {
                        NavigationService.ShowLimitReached(new PremiumLimitTypeShareableChatFolderCount());
                    }
                }
            }
            else
            {
                OpenLink(null);
            }
        }
    }

    public class ChatFolderElement
    {
    }

    public class FolderFlag : ChatFolderElement
    {
        public ChatListFolderFlags Flag { get; set; }
    }

    public class FolderChat : ChatFolderElement
    {
        public Chat Chat { get; set; }
    }
}
