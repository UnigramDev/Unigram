//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class ChatJoinRequestsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private readonly Chat _chat;
        private readonly string _inviteLink;

        public ChatJoinRequestsViewModel(Chat chat, string inviteLink, IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _chat = chat;
            _inviteLink = inviteLink;

            Items = new IncrementalCollection<ChatJoinRequest>(this);

            AcceptCommand = new RelayCommand<ChatJoinRequest>(Accept);
            DismissCommand = new RelayCommand<ChatJoinRequest>(Dismiss);
        }

        public bool IsChannel => _chat?.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel;

        public IncrementalCollection<ChatJoinRequest> Items { get; private set; }

        public RelayCommand<ChatJoinRequest> AcceptCommand { get; }
        private void Accept(ChatJoinRequest request)
        {
            Process(request, true);
        }

        public RelayCommand<ChatJoinRequest> DismissCommand { get; }
        private void Dismiss(ChatJoinRequest request)
        {
            Process(request, false);
        }

        private void Process(ChatJoinRequest request, bool approve)
        {
            Items.Remove(request);
            ClientService.Send(new ProcessChatJoinRequest(_chat.Id, request.UserId, approve));
        }

        #region IIncrementalCollectionOwner

        private ChatJoinRequest _offset;
        private bool _hasMoreItems = true;

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetChatJoinRequests(_chat.Id, _inviteLink, string.Empty, _offset, 10));
            if (response is ChatJoinRequests requests)
            {
                foreach (var item in requests.Requests)
                {
                    _offset = item;

                    Items.Add(item);
                    totalCount++;
                }

                _hasMoreItems = requests.Requests.Count > 0;
            }

            return new LoadMoreItemsResult { Count = totalCount };
        }

        public bool HasMoreItems => _hasMoreItems;

        #endregion
    }
}
