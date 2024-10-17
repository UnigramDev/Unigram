//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Collection;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public partial class FolderViewModel : ViewModelBase, IDiffHandler<ChatFolderElement>
    {
        public FolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Include = new BatchedObservableCollection<ChatFolderElement>(8, this, Constants.DiffOptions);
            Exclude = new BatchedObservableCollection<ChatFolderElement>(8, this, Constants.DiffOptions);

            AvailableColors = new ObservableCollection<NameColor>(ClientService.GetAvailableAccentColors()
                .Where(x => x.Id == x.BuiltInAccentColorId)
                .Append(new NameColor(-1)));

            Links = new MvxObservableCollection<ChatFolderInviteLink>();
        }

        public bool CompareItems(ChatFolderElement oldItem, ChatFolderElement newItem)
        {
            if (oldItem is FolderFlag oldFlag && newItem is FolderFlag newFlag)
            {
                return oldFlag.Flag == newFlag.Flag;
            }
            else if (oldItem is FolderChat oldChat && newItem is FolderChat newChat)
            {
                return oldChat.ChatId == newChat.ChatId;
            }

            return false;
        }

        public void UpdateItem(ChatFolderElement oldItem, ChatFolderElement newItem)
        {

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
                    IsShareable = result.IsShareable;
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
                IsShareable = false;
                Folder = null;
                folder = new ChatFolder();
                folder.PinnedChatIds = new List<long>();
                folder.IncludedChatIds = new List<long>();
                folder.ExcludedChatIds = new List<long>();

                if (parameter is FolderPageCreateArgs createArgs)
                {
                    folder.IncludedChatIds.Add(createArgs.IncludeChatId);
                }
            }

            if (folder == null)
            {
                return;
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

            var includeFlags = new List<ChatFolderElement>();
            var includeChats = new List<FolderChat>();

            var excludeFlags = new List<ChatFolderElement>();
            var excludeChats = new List<FolderChat>();

            if (folder.IncludeContacts) includeFlags.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
            if (folder.IncludeNonContacts) includeFlags.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));
            if (folder.IncludeGroups) includeFlags.Add(new FolderFlag(ChatListFolderFlags.IncludeGroups));
            if (folder.IncludeChannels) includeFlags.Add(new FolderFlag(ChatListFolderFlags.IncludeChannels));
            if (folder.IncludeBots) includeFlags.Add(new FolderFlag(ChatListFolderFlags.IncludeBots));

            if (folder.ExcludeMuted) excludeFlags.Add(new FolderFlag(ChatListFolderFlags.ExcludeMuted));
            if (folder.ExcludeRead) excludeFlags.Add(new FolderFlag(ChatListFolderFlags.ExcludeRead));
            if (folder.ExcludeArchived) excludeFlags.Add(new FolderFlag(ChatListFolderFlags.ExcludeArchived));

            foreach (var chatId in folder.PinnedChatIds.Union(folder.IncludedChatIds))
            {
                includeChats.Add(new FolderChat(chatId));
            }

            foreach (var chatId in folder.ExcludedChatIds)
            {
                excludeChats.Add(new FolderChat(chatId));
            }

            Include.ReplaceDiff(includeFlags.Union(includeChats.OrderBy(x => x.ChatId)));
            Exclude.ReplaceDiff(excludeFlags.Union(excludeChats.OrderBy(x => x.ChatId)));

            UpdateIcon();
            UpdateLinks();

            RaisePropertyChanged(nameof(HasChanged));
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (!_completed && HasChanged)
            {
                var message = Id.HasValue
                    ? Strings.FilterDiscardAlert
                    : Strings.FilterDiscardNewAlert;

                var title = Id.HasValue
                    ? Strings.FilterDiscardTitle
                    : Strings.FilterDiscardNewTitle;

                var primary = Id.HasValue
                    ? Strings.ApplyTheme
                    : Strings.FilterDiscardNewSave;

                args.Cancel = true;

                var confirm = await ShowPopupAsync(message, title, primary, Strings.PassportDiscard);
                if (confirm == ContentDialogResult.Primary)
                {
                    Continue();
                }
                else if (confirm == ContentDialogResult.Secondary)
                {
                    _completed = true;
                    NavigationService.GoBack();
                }
            }
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
            set => Invalidate(ref _title, value);
        }

        private bool _isShareable;
        public bool IsShareable
        {
            get => _isShareable;
            set => Invalidate(ref _isShareable, value);
        }

        private bool _iconPicked;
        private int _originalColorId = -1;

        private ChatFolderIcon2 _icon;
        public ChatFolderIcon2 Icon
        {
            get => _icon;
            private set => Invalidate(ref _icon, value);
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
            set => Invalidate(ref _selectedColor, value);
        }

        private async void UpdateLinks()
        {
            if (Id is int id)
            {
                var response = await ClientService.SendAsync(new GetChatFolderInviteLinks(id));
                if (response is ChatFolderInviteLinks links && links.InviteLinks.Count > 0)
                {
                    Links.ReplaceWith(links.InviteLinks);
                    Exclude.Clear();
                    Exclude.SynchronizeHead();

                    IsShareable = true;
                }
                else
                {
                    Links.Clear();
                    IsShareable = false;
                }
            }
        }

        private IList<long> _pinnedChatIds = Array.Empty<long>();

        public BatchedObservableCollection<ChatFolderElement> Include { get; private set; }
        public BatchedObservableCollection<ChatFolderElement> Exclude { get; private set; }

        public MvxObservableCollection<ChatFolderInviteLink> Links { get; private set; }

        public async void AddIncluded()
        {
            await AddIncludeAsync();
            UpdateIcon();

            RaisePropertyChanged(nameof(HasChanged));
        }

        public async Task AddIncludeAsync()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, true, _folder == null || (!_folder.IsShareable && Links.Count == 0), false, Include.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FolderChat>())
                {
                    var already = Exclude.OfType<FolderChat>().FirstOrDefault(x => x.ChatId == item.ChatId);
                    if (already != null)
                    {
                        Exclude.Remove(already);
                    }
                }

                var flags = result.OfType<FolderFlag>().Cast<ChatFolderElement>();
                var chats = result.OfType<FolderChat>().OrderBy(x => x.ChatId);

                Include.ReplaceDiff(flags.Union(chats));
                Exclude.SynchronizeHead();
            }
        }

        public async void AddExcluded()
        {
            await AddExcludeAsync();
            UpdateIcon();

            RaisePropertyChanged(nameof(HasChanged));
        }

        public async Task AddExcludeAsync()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, false, true, false, Exclude.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FolderChat>())
                {
                    var already = Include.OfType<FolderChat>().FirstOrDefault(x => x.ChatId == item.ChatId);
                    if (already != null)
                    {
                        Include.Remove(already);
                    }
                }

                var flags = result.OfType<FolderFlag>().Cast<ChatFolderElement>();
                var chats = result.OfType<FolderChat>().OrderBy(x => x.ChatId);

                Include.SynchronizeHead();
                Exclude.ReplaceDiff(flags.Union(chats));
            }
        }

        public void RemoveIncluded(ChatFolderElement chat)
        {
            Include.Remove(chat);
            Include.SynchronizeHead();
            UpdateIcon();

            RaisePropertyChanged(nameof(HasChanged));
        }

        public void RemoveExcluded(ChatFolderElement chat)
        {
            Exclude.Remove(chat);
            Exclude.SynchronizeHead();
            UpdateIcon();

            RaisePropertyChanged(nameof(HasChanged));
        }

        private ChatFolder _cached;

        private bool _completed;
        public bool HasChanged => CanBeSaved && (_folder == null || !_folder.AreTheSame(GetFolder()));

        public bool CanBeSaved => !string.IsNullOrEmpty(Title) && Include.Count > 0;

        protected bool Invalidate<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Set(ref storage, value, propertyName))
            {
                RaisePropertyChanged(nameof(HasChanged));
                return true;
            }

            return false;
        }

        public async void Continue()
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

        private ChatFolder GetFolder()
        {
            var folder = new ChatFolder();
            folder.Title = Title ?? string.Empty;
            folder.Icon = new ChatFolderIcon(_iconPicked ? Enum.GetName(typeof(ChatFolderIcon2), Icon) : string.Empty);
            folder.ColorId = IsPremium ? SelectedColor?.Id ?? -1 : _originalColorId;
            folder.IsShareable = IsShareable;
            folder.PinnedChatIds = new List<long>();
            folder.IncludedChatIds = new List<long>();
            folder.ExcludedChatIds = new List<long>();

            foreach (var item in _pinnedChatIds)
            {
                if (Include.Contains(new FolderChat(item)))
                {
                    folder.PinnedChatIds.Add(item);
                }
            }

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
                else if (item is FolderChat chat && !folder.PinnedChatIds.Contains(chat.ChatId))
                {
                    folder.IncludedChatIds.Add(chat.ChatId);
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
                    folder.ExcludedChatIds.Add(chat.ChatId);
                }
            }

            return folder;
        }

        public async void OpenLink(ChatFolderInviteLink link)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await ShowPopupAsync(new ShareFolderPopup(tsc), Tuple.Create(Id.Value, link));
            if (confirm == ContentDialogResult.Primary)
            {
                var result = await tsc.Task;
                if (result is IList<long> chats)
                {
                    if (link != null)
                    {
                        result = await ClientService.SendAsync(new EditChatFolderInviteLink(Id.Value, link.InviteLink, string.Empty, chats));
                    }
                    else
                    {
                        result = await ClientService.SendAsync(new CreateChatFolderInviteLink(Id.Value, string.Empty, chats));
                    }

                    if (result is ChatFolderInviteLink inviteLink)
                    {
                        Links.Insert(0, inviteLink);
                        Exclude.Clear();
                        Exclude.SynchronizeHead();

                        IsShareable = true;
                    }
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

            var shareableItems = Include.OfType<FolderChat>()
                .Select(x => x.ChatId)
                .ToList();

            for (int i = 0; i < shareableItems.Count; i++)
            {
                var chat = ClientService.GetChat(shareableItems[i]);
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

                shareableItems.RemoveAt(i);
                i--;
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

    public record ChatFolderElement;

    public record FolderFlag(ChatListFolderFlags Flag) : ChatFolderElement;

    public record FolderChat(long ChatId) : ChatFolderElement;
}
