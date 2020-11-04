using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Folders
{
    public class FolderViewModel : TLViewModelBase
    {
        public FolderViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Include = new MvxObservableCollection<ChatFilterElement>();
            Exclude = new MvxObservableCollection<ChatFilterElement>();

            Include.CollectionChanged += OnCollectionChanged;
            Exclude.CollectionChanged += OnCollectionChanged;

            RemoveIncludeCommand = new RelayCommand<ChatFilterElement>(RemoveIncludeExecute);
            RemoveExcludeCommand = new RelayCommand<ChatFilterElement>(RemoveExcludeExecute);

            AddIncludeCommand = new RelayCommand(AddIncludeExecute);
            AddExcludeCommand = new RelayCommand(AddExcludeExecute);

            SendCommand = new RelayCommand(SendExecute, SendCanExecute);
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SendCommand.RaiseCanExecuteChanged();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ChatFilter filter = null;

            if (parameter is int id)
            {
                var response = await ProtoService.SendAsync(new GetChatFilter(id));
                if (response is ChatFilter result)
                {
                    Id = id;
                    Filter = result;
                    filter = result;
                }
                else
                {
                    // TODO
                }
            }
            else
            {
                Id = null;
                Filter = null;
                filter = new ChatFilter();
                filter.PinnedChatIds = new List<long>();
                filter.IncludedChatIds = new List<long>();
                filter.ExcludedChatIds = new List<long>();
            }

            if (filter == null)
            {
                return;
            }

            if (state != null && state.TryGet("included_chat_id", out long includedChatId))
            {
                filter.IncludedChatIds.Add(includedChatId);
            }

            _pinnedChatIds = filter.PinnedChatIds;

            _iconPicked = !string.IsNullOrEmpty(filter.IconName);

            Title = filter.Title;
            Icon = Icons.ParseFilter(filter);

            Include.Clear();
            Exclude.Clear();

            if (filter.IncludeContacts) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeContacts });
            if (filter.IncludeNonContacts) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeNonContacts });
            if (filter.IncludeGroups) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeGroups });
            if (filter.IncludeChannels) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeChannels });
            if (filter.IncludeBots) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeBots });

            if (filter.ExcludeMuted) Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeMuted });
            if (filter.ExcludeRead) Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeRead });
            if (filter.ExcludeArchived) Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeArchived });

            foreach (var chatId in filter.PinnedChatIds.Union(filter.IncludedChatIds))
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Include.Add(new FilterChat { Chat = chat });
            }

            foreach (var chatId in filter.ExcludedChatIds)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                Exclude.Add(new FilterChat { Chat = chat });
            }

            UpdateIcon();
        }

        public int? Id { get; set; }

        private ChatFilter _filter;
        public ChatFilter Filter
        {
            get => _filter;
            set => Set(ref _filter, value);
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

        private ChatFilterIcon _icon;
        public ChatFilterIcon Icon
        {
            get => _icon;
            private set => Set(ref _icon, value);
        }

        public void SetIcon(ChatFilterIcon icon)
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

            Icon = Icons.ParseFilter(GetFilter());
        }

        private IList<long> _pinnedChatIds;

        public MvxObservableCollection<ChatFilterElement> Include { get; private set; }
        public MvxObservableCollection<ChatFilterElement> Exclude { get; private set; }



        public RelayCommand AddIncludeCommand { get; }
        private async void AddIncludeExecute()
        {
            await AddIncludeAsync();
            UpdateIcon();
        }

        public async Task AddIncludeAsync()
        {
            var result = await SharePopup.AddExecute(true, Include.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FilterChat>())
                {
                    var already = Exclude.OfType<FilterChat>().FirstOrDefault(x => x.Chat.Id == item.Chat.Id);
                    if (already != null)
                    {
                        Exclude.Remove(already);
                    }
                }

                var flags = result.OfType<FilterFlag>().Cast<ChatFilterElement>();
                var chats = result.OfType<FilterChat>().OrderBy(x =>
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

        public RelayCommand AddExcludeCommand { get; }
        private async void AddExcludeExecute()
        {
            await AddExcludeAsync();
            UpdateIcon();
        }

        public async Task AddExcludeAsync()
        {
            var result = await SharePopup.AddExecute(false, Exclude.ToList());
            if (result != null)
            {
                foreach (var item in result.OfType<FilterChat>())
                {
                    var already = Include.OfType<FilterChat>().FirstOrDefault(x => x.Chat.Id == item.Chat.Id);
                    if (already != null)
                    {
                        Include.Remove(already);
                    }
                }

                Exclude.ReplaceWith(result);
            }
        }

        public RelayCommand<ChatFilterElement> RemoveIncludeCommand { get; }
        private void RemoveIncludeExecute(ChatFilterElement chat)
        {
            Include.Remove(chat);
            UpdateIcon();
        }

        public RelayCommand<ChatFilterElement> RemoveExcludeCommand { get; }
        private void RemoveExcludeExecute(ChatFilterElement chat)
        {
            Exclude.Remove(chat);
            UpdateIcon();
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await SendAsync();
            if (response is ChatFilterInfo)
            {
                NavigationService.GoBack();
            }
        }

        public Task<BaseObject> SendAsync()
        {
            Function function;
            if (Id is int id)
            {
                function = new EditChatFilter(id, GetFilter());
            }
            else
            {
                function = new CreateChatFilter(GetFilter());
            }

            return ProtoService.SendAsync(function);
        }

        private bool SendCanExecute()
        {
            return !string.IsNullOrEmpty(Title) && Include.Count > 0;
        }

        private ChatFilter GetFilter()
        {
            var filter = new ChatFilter();
            filter.Title = Title ?? string.Empty;
            filter.IconName = _iconPicked ? Enum.GetName(typeof(ChatFilterIcon), Icon) : string.Empty;
            filter.PinnedChatIds = new List<long>();
            filter.IncludedChatIds = new List<long>();
            filter.ExcludedChatIds = new List<long>();

            foreach (var item in Include)
            {
                if (item is FilterFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFilterFlags.IncludeContacts:
                            filter.IncludeContacts = true;
                            break;
                        case ChatListFilterFlags.IncludeNonContacts:
                            filter.IncludeNonContacts = true;
                            break;
                        case ChatListFilterFlags.IncludeGroups:
                            filter.IncludeGroups = true;
                            break;
                        case ChatListFilterFlags.IncludeChannels:
                            filter.IncludeChannels = true;
                            break;
                        case ChatListFilterFlags.IncludeBots:
                            filter.IncludeBots = true;
                            break;
                    }
                }
                else if (item is FilterChat chat)
                {
                    if (_pinnedChatIds.Contains(chat.Chat.Id))
                    {
                        filter.PinnedChatIds.Add(chat.Chat.Id);
                    }
                    else
                    {
                        filter.IncludedChatIds.Add(chat.Chat.Id);
                    }
                }
            }

            foreach (var item in Exclude)
            {
                if (item is FilterFlag flag)
                {
                    switch (flag.Flag)
                    {
                        case ChatListFilterFlags.ExcludeMuted:
                            filter.ExcludeMuted = true;
                            break;
                        case ChatListFilterFlags.ExcludeRead:
                            filter.ExcludeRead = true;
                            break;
                        case ChatListFilterFlags.ExcludeArchived:
                            filter.ExcludeArchived = true;
                            break;
                    }
                }
                else if (item is FilterChat chat)
                {
                    filter.ExcludedChatIds.Add(chat.Chat.Id);
                }
            }

            return filter;
        }
    }

    public class ChatFilterElement
    {
    }

    public class FilterFlag : ChatFilterElement
    {
        public ChatListFilterFlags Flag { get; set; }
    }

    public class FilterChat : ChatFilterElement
    {
        public Chat Chat { get; set; }
    }
}
