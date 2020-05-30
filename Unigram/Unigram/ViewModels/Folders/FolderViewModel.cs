using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views.Popups;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.Xaml.Controls;
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
                filter.IncludedChatIds = new List<long>();
                filter.ExcludedChatIds = new List<long>();
            }

            if (filter == null)
            {
                return;
            }

            Title = filter.Title;
            Emoji = filter.Emoji ?? ChatFilterIcon.Default;

            Include.Clear();
            Exclude.Clear();

            if (filter.IncludeContacts)    Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeContacts });
            if (filter.IncludeNonContacts) Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeNonContacts });
            if (filter.IncludeGroups)      Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeGroups });
            if (filter.IncludeChannels)    Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeChannels });
            if (filter.IncludeBots)        Include.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeBots });

            if (filter.ExcludeMuted)       Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeMuted});
            if (filter.ExcludeRead)        Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeRead });
            if (filter.ExcludeArchived)    Exclude.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeArchived });

            foreach (var chatId in filter.IncludedChatIds)
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
        }

        public int? Id { get; set; }

        private ChatFilter _filter;
        public ChatFilter Filter
        {
            get => _filter;
            set => Set(ref _filter, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _emoji;
        public string Emoji
        {
            get => _emoji;
            set => Set(ref _emoji, value);
        }

        public MvxObservableCollection<ChatFilterElement> Include { get; private set; }
        public MvxObservableCollection<ChatFilterElement> Exclude { get; private set; }



        public RelayCommand AddIncludeCommand { get; }
        private async void AddIncludeExecute()
        {
            await AddIncludeAsync();
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

                Include.ReplaceWith(result);
            }
        }

        public RelayCommand AddExcludeCommand { get; }
        private async void AddExcludeExecute()
        {
            await AddExcludeAsync();
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

        private void Header_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var filter = args.Item as FilterFlag;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var title = content.Children[1] as TextBlock;
            //title.Text = Enum.GetName(typeof(ChatListFilterFlags), filter.Flag);

            switch (filter.Flag)
            {
                case ChatListFilterFlags.IncludeContacts:
                    title.Text = Strings.Resources.FilterContacts;
                    break;
                case ChatListFilterFlags.IncludeNonContacts:
                    title.Text = Strings.Resources.FilterNonContacts;
                    break;
                case ChatListFilterFlags.IncludeGroups:
                    title.Text = Strings.Resources.FilterGroups;
                    break;
                case ChatListFilterFlags.IncludeChannels:
                    title.Text = Strings.Resources.FilterChannels;
                    break;
                case ChatListFilterFlags.IncludeBots:
                    title.Text = Strings.Resources.FilterBots;
                    break;

                case ChatListFilterFlags.ExcludeMuted:
                    title.Text = Strings.Resources.FilterMuted;
                    break;
                case ChatListFilterFlags.ExcludeRead:
                    title.Text = Strings.Resources.FilterRead;
                    break;
                case ChatListFilterFlags.ExcludeArchived:
                    title.Text = Strings.Resources.FilterArchived;
                    break;
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(filter.Flag), (int)filter.Flag, 36);
        }

        public RelayCommand<ChatFilterElement> RemoveIncludeCommand { get; }
        private void RemoveIncludeExecute(ChatFilterElement chat)
        {
            Include.Remove(chat);
        }

        public RelayCommand<ChatFilterElement> RemoveExcludeCommand { get; }
        private void RemoveExcludeExecute(ChatFilterElement chat)
        {
            Exclude.Remove(chat);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await SendAsync();
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
        }

        public Task<BaseObject> SendAsync()
        {
            var include = new List<long>();
            var exclude = new List<long>();

            var filter = new ChatFilter();
            filter.Title = Title;
            filter.Emoji = Emoji;

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
                    include.Add(chat.Chat.Id);
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
                    exclude.Add(chat.Chat.Id);
                }
            }

            filter.IncludedChatIds = include;
            filter.ExcludedChatIds = exclude;

            Function function;
            if (Id is int id)
            {
                function = new EditChatFilter(id, filter);
            }
            else
            {
                function = new CreateChatFilter(filter);
            }

            return ProtoService.SendAsync(function);
        }

        private bool SendCanExecute()
        {
            return !string.IsNullOrEmpty(Title) &&
                Include.Count > 0 ||
                Exclude.Count > 0;
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
