using Rg.DiffUtils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessBotsViewModel : BusinessFeatureViewModelBase
    {
        public BusinessBotsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Results = new SearchCollection<User, BotsCollection>(UpdateResults, new UserDiffHandler());
        }

        class UserDiffHandler : IDiffHandler<User>
        {
            public bool CompareItems(User oldItem, User newItem)
            {
                return oldItem.Id == newItem.Id;
            }

            public void UpdateItem(User oldItem, User newItem)
            {

            }
        }

        public BotsCollection UpdateResults(object sender, string value)
        {
            return new BotsCollection(ClientService, value);
        }

        private long _botUserId;
        public long BotUserId
        {
            get => _botUserId;
            set => Invalidate(ref _botUserId, value);
        }

        public void Clear()
        {
            BotUserId = 0;
        }

        public SearchCollection<User, BotsCollection> Results { get; private set; }

        private bool _canReply = true;
        public bool CanReply
        {
            get => _canReply;
            set => Invalidate(ref _canReply, value);
        }

        protected override async Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetBusinessConnectedBot());
            if (response is BusinessConnectedBot connectedBot)
            {
                _cached = connectedBot;

                BotUserId = connectedBot.BotUserId;
                CanReply = connectedBot.CanReply;

                UpdateRecipients(connectedBot.Recipients);
            }
        }

        public bool IsExclude
        {
            get => _recipientsType == BusinessRecipientsType.Exclude;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Exclude);
                }
            }
        }

        public bool IsInclude
        {
            get => _recipientsType == BusinessRecipientsType.Include;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Include);
                }
            }
        }

        private BusinessRecipientsType _recipientsType;
        public BusinessRecipientsType RecipientsType
        {
            get => _recipientsType;
            set => SetRecipientsType(value);
        }

        private void SetRecipientsType(BusinessRecipientsType value, bool update = true)
        {
            if (Invalidate(ref _recipientsType, value, nameof(RecipientsType)))
            {
                IncludedChats.Clear();
                ExcludedChats.Clear();

                RaisePropertyChanged(nameof(IsExclude));
                RaisePropertyChanged(nameof(IsInclude));
            }
        }



        public MvxObservableCollection<ChatFolderElement> ExcludedChats { get; } = new();
        public MvxObservableCollection<ChatFolderElement> IncludedChats { get; } = new();

        public async void AddExcluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, false, IsExclude, true, ExcludedChats.ToList());
            if (result != null)
            {
                ExcludedChats.ReplaceWith(result);

                var ids = result
                    .OfType<FolderChat>()
                    .Select(x => x.ChatId)
                    .ToHashSet();

                var excluded = IncludedChats
                    .OfType<FolderChat>()
                    .ToList();

                foreach (var item in excluded)
                {
                    if (ids.Contains(item.ChatId))
                    {
                        IncludedChats.Remove(item);
                    }
                }

                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public async void AddIncluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, true, true, true, IncludedChats.ToList());
            if (result != null)
            {
                IncludedChats.ReplaceWith(result);

                var ids = result
                    .OfType<FolderChat>()
                    .Select(x => x.ChatId)
                    .ToHashSet();

                var excluded = ExcludedChats
                    .OfType<FolderChat>()
                    .ToList();

                foreach (var item in excluded)
                {
                    if (ids.Contains(item.ChatId))
                    {
                        ExcludedChats.Remove(item);
                    }
                }

                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public void RemoveIncluded(ChatFolderElement chat)
        {
            IncludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        public void RemoveExcluded(ChatFolderElement chat)
        {
            ExcludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        protected void UpdateRecipients(BusinessRecipients recipients)
        {
            SetRecipientsType(recipients.ExcludeSelected
                ? BusinessRecipientsType.Exclude
                : BusinessRecipientsType.Include);

            IncludedChats.Clear();
            ExcludedChats.Clear();

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            if (recipients.SelectExistingChats) target.Add(new FolderFlag(ChatListFolderFlags.ExistingChats));
            if (recipients.SelectNewChats) target.Add(new FolderFlag(ChatListFolderFlags.NewChats));
            if (recipients.SelectContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
            if (recipients.SelectNonContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));

            foreach (var chatId in recipients.ChatIds)
            {
                IncludedChats.Add(new FolderChat(chatId));
            }

            foreach (var chatId in recipients.ExcludedChatIds)
            {
                ExcludedChats.Add(new FolderChat(chatId));
            }

            RaisePropertyChanged(nameof(HasChanged));
        }

        protected BusinessRecipients GetRecipients()
        {
            var recipients = new BusinessRecipients
            {
                ExcludeSelected = RecipientsType == BusinessRecipientsType.Exclude,
                ChatIds = new List<long>(),
                ExcludedChatIds = new List<long>()
            };

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            foreach (var item in target)
            {
                if (item is FolderFlag flag)
                {
                    if (flag.Flag == ChatListFolderFlags.IncludeContacts) recipients.SelectContacts = true;
                    if (flag.Flag == ChatListFolderFlags.IncludeNonContacts) recipients.SelectNonContacts = true;
                    if (flag.Flag == ChatListFolderFlags.ExistingChats) recipients.SelectExistingChats = true;
                    if (flag.Flag == ChatListFolderFlags.NewChats) recipients.SelectNewChats = true;
                }
            }

            foreach (var item in IncludedChats)
            {
                if (item is FolderChat chat)
                {
                    recipients.ChatIds.Add(chat.ChatId);
                }
            }

            foreach (var item in ExcludedChats)
            {
                if (item is FolderChat chat)
                {
                    recipients.ExcludedChatIds.Add(chat.ChatId);
                }
            }

            return recipients;
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        public override async void Continue()
        {
            _completed = true;

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(settings == null
                ? new DeleteBusinessConnectedBot()
                : new SetBusinessConnectedBot(settings));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private BusinessConnectedBot _cached;
        private BusinessConnectedBot GetSettings()
        {
            if (BotUserId == 0)
            {
                return null;
            }

            return new BusinessConnectedBot
            {
                BotUserId = BotUserId,
                CanReply = CanReply,
                Recipients = GetRecipients()
            };
        }

        public partial class BotsCollection : ObservableCollection<User>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly string _query;

            private readonly HashSet<long> _ids = new();

            public BotsCollection(IClientService clientService, string query)
            {
                _clientService = clientService;
                _query = query;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var totalCount = 0u;

                    void ProcessResult(object result)
                    {
                        if (result is Td.Api.Chats chats)
                        {
                            foreach (var chat in _clientService.GetChats(chats.ChatIds))
                            {
                                if (_clientService.TryGetUser(chat, out User user))
                                {
                                    if (user.Type is UserTypeBot && !_ids.Contains(user.Id))
                                    {
                                        _ids.Add(user.Id);

                                        Add(user);
                                        totalCount++;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(_query))
                    {
                        var response1 = await _clientService.SendAsync(new SearchChats(_query, 50));
                        ProcessResult(response1);

                        var response2 = await _clientService.SendAsync(new SearchChatsOnServer(_query, 50));
                        ProcessResult(response2);

                        var response3 = await _clientService.SendAsync(new SearchPublicChats(_query));
                        ProcessResult(response3);
                    }

                    HasMoreItems = false;

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                });
            }

            public bool HasMoreItems { get; private set; } = true;
        }
    }
}
