using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class MessageInteractionsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private MessageViewModel _message;
        private string _nextOffset;

        private HashSet<long> _users = new();

        public MessageInteractionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<object>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is MessageViewModel message)
            {
                _message = message;
                _nextOffset = string.Empty;
            }

            return Task.CompletedTask;
        }

        public IncrementalCollection<object> Items { get; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            if (_nextOffset != null)
            {
                var response = await ClientService.SendAsync(new GetMessageAddedReactions(_message.ChatId, _message.Id, null, _nextOffset, 50));
                if (response is AddedReactions addedReactions)
                {
                    _nextOffset = addedReactions.NextOffset.Length > 0 ? addedReactions.NextOffset : null;
                    
                    foreach (var item in addedReactions.Reactions)
                    {
                        if (item.SenderId is MessageSenderUser senderUser)
                        {
                            _users.Add(senderUser.UserId);
                        }

                        totalCount++;
                        Items.Add(item);
                    }
                }
            }
            else
            {
                var response = await ClientService.SendAsync(new GetMessageViewers(_message.ChatId, _message.Id));
                if (response is MessageViewers viewers)
                {
                    HasMoreItems = false;
                    //Items.AddRange(viewers.Viewers);

                    foreach (var item in viewers.Viewers)
                    {
                        if (_users.Contains(item.UserId))
                        {
                            continue;
                        }

                        totalCount++;
                        Items.Add(item);
                    }
                }
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
