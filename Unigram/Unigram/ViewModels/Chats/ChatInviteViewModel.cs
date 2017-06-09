using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteViewModel : UsersSelectionViewModel
    {
        public ChatInviteViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLChatBase _item;
        public TLChatBase Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        public bool IsCreator
        {
            get
            {
                return _item != null && ((_item is TLChannel channel && channel.IsCreator) || (_item is TLChat chat && chat.IsCreator));
            }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Item = null;

            var chat = parameter as TLChatBase;
            var peer = parameter as TLPeerBase;
            if (peer != null)
            {
                chat = CacheService.GetChat(peer.Id);
            }

            if (chat != null)
            {
                Item = chat;
                RaisePropertyChanged(() => IsCreator);
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }
    }
}
