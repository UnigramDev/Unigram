//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public class FolderViewModel : TLViewModelBase
    {
        public FolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Include = new MvxObservableCollection<ChatFolderElement>();
            Exclude = new MvxObservableCollection<ChatFolderElement>();

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
                    Folder = result;
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

            _iconPicked = !string.IsNullOrEmpty(folder.Icon.Name);

            Title = folder.Title;
            Icon = Icons.ParseFolder(folder);

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

        private IList<long> _pinnedChatIds;

        public MvxObservableCollection<ChatFolderElement> Include { get; private set; }
        public MvxObservableCollection<ChatFolderElement> Exclude { get; private set; }



        public async void AddIncluded()
        {
            await AddIncludeAsync();
            UpdateIcon();
        }

        public async Task AddIncludeAsync()
        {
            var result = await SharePopup.AddExecute(true, Include.ToList());
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
            var result = await SharePopup.AddExecute(false, Exclude.ToList());
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
