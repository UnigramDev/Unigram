//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Chats;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class MessageStatisticsViewModel : TLViewModelBase
    {
        public MessageStatisticsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private Message _message;
        public Message Message
        {
            get => _message;
            set => Set(ref _message, value);
        }

        private ChartViewData _interactions;
        public ChartViewData Interactions
        {
            get => _interactions;
            set => Set(ref _interactions, value);
        }

        private ItemsCollection _items;
        public ItemsCollection Items
        {
            get => _items;
            set => Set(ref _items, value);
        }

        public void OpenChannel()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatStatisticsPage), chat.Id);
        }

        public void OpenPost(Message message)
        {
            NavigationService.NavigateToChat(message.ChatId, message.Id);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var data = (string)parameter;

            var split = data.Split(';');
            if (split.Length != 2)
            {
                return;
            }

            var failed1 = !long.TryParse(split[0], out long chatId);
            var failed2 = !long.TryParse(split[1], out long messageId);

            if (failed1 || failed2)
            {
                return;
            }

            IsLoading = true;

            Chat = ClientService.GetChat(chatId);

            Items = new ItemsCollection(ClientService, chatId, messageId);

            var message = await ClientService.SendAsync(new GetMessage(chatId, messageId));
            if (message is Message result)
            {
                Message = result;
            }

            var response = await ClientService.SendAsync(new GetMessageStatistics(chatId, messageId, false));
            if (response is MessageStatistics statistics)
            {
                Interactions = ChartViewData.Create(statistics.MessageInteractionGraph, Strings.InteractionsChartTitle, /*1*/6);
            }

            IsLoading = false;
        }

        public class ItemsCollection : MvxObservableCollection<Message>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;

            private readonly long _chatId;
            private readonly long _messageId;

            private string _nextOffset;

            public ItemsCollection(IClientService clientService, long chatId, long messageId)
            {
                _clientService = clientService;

                _chatId = chatId;
                _messageId = messageId;

                _nextOffset = string.Empty;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(token => LoadMoreItemsAsync());
            }

            private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
            {
                var response = await _clientService.SendAsync(new GetMessagePublicForwards(_chatId, _messageId, _nextOffset, 50));
                if (response is FoundMessages messages)
                {
                    foreach (var message in messages.Messages)
                    {
                        Add(message);
                    }

                    if (string.IsNullOrEmpty(messages.NextOffset))
                    {
                        _nextOffset = null;
                    }
                    else
                    {
                        _nextOffset = messages.NextOffset;
                    }

                    if (messages.TotalCount > 0)
                    {
                        TotalCount = messages.TotalCount;
                    }

                    return new LoadMoreItemsResult { Count = (uint)messages.Messages.Count };
                }

                return new LoadMoreItemsResult();
            }

            public bool HasMoreItems => _nextOffset != null;

            private int _totalCount;
            public int TotalCount
            {
                get => _totalCount;
                set => Set(ref _totalCount, value);
            }
        }
    }
}
