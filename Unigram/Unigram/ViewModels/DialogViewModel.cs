using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;
using Template10.Common;
using Telegram.Api.Helpers;
using Unigram.Core.Services;
using Telegram.Api.Services.Updates;
using Telegram.Api.Transport;
using Telegram.Api.Services.Connection;
using System.Threading;
using GalaSoft.MvvmLight.Command;
using System.Collections.ObjectModel;

namespace Unigram.ViewModels
{

    public class DialogViewModel : UnigramViewModelBase
    {
        int ChatType=-1;
        int counter = 0;
        //0 if private, 1 if group, 2 if supergroup/channel
        int loadCount =15;
        int loaded = 0;
        public TLPeerBase peer;
        public TLInputPeerBase inputPeer;
        public ObservableCollection<TLMessage> ListX= new ObservableCollection<TLMessage>();
        public string DialogTitle;
        public string debug;
        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public string SendTextHolder;
        public TLUser user;
        private TLUserBase _item;
        public TLUserBase Item
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
        public TLUserBase Self
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
        public TLInputPeerChannel channel;
        private TLPeerChannel _channelItem;
        public TLPeerChannel channelItem
         {
            get
            {
                return _channelItem;
            }
             set
            {
                Set(ref _channelItem, value);
            }
        }
        public TLInputPeerChat chat;
        private TLPeerChat _chatItem;
        public TLPeerChat chatItem
        {
            get
            {
                return _chatItem;
            }
            set
            {
                Set(ref _chatItem, value);
            }
        }

        private TLInputPeerBase _peerBase;
        public TLInputPeerBase selfPeerBase
        {
            get
            {
                return _peerBase;
            }
            set
            {
                Set(ref _peerBase, value);
            }
        }
        private TLPeerBase _inputpeerBase;
        public TLPeerBase idPeerBase
        {
            get
            {
                return _inputpeerBase;
            }
            set
            {
                Set(ref _inputpeerBase, value);
            }
        }
        public async Task FetchMessages(TLPeerBase peer, TLInputPeerBase inputPeer)
        {
            List<string> fetchedList = new List<string>();
            var x = await ProtoService.GetHistoryAsync(null, inputPeer, peer, false, loaded, int.MaxValue, loadCount);
            TLVector<TLMessageBase> y = x.Value.Messages;
            foreach (var item in y)
            {                
                var xy = (TLMessage)item;
               // if (xy.Id == SettingsHelper.UserId)
                    //set the thing to right alignment
                //var time = TLUtils.ToDateTime(xy.Date);
                ListX.Insert(0, xy);

                counter++;
            }
            loaded += loadCount;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            loaded = 0;
            channel = parameter as TLInputPeerChannel;
            chat = parameter as TLInputPeerChat;
            user = parameter as TLUser;
            if (user != null)
            {
                //Happy Birthday Alexmitter xD
                ListX.Clear();
                Item = user;
                DialogTitle = Item.FullName;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerUser { UserId = user.Id };
                await FetchMessages(peer,inputPeer);
                ChatType = 0;
            }
            else if (channel != null)
            {

                TLInputChannel x=new TLInputChannel();
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var channelDetails = await ProtoService.GetFullChannelAsync(x);
                DialogTitle = channelDetails.Value.Chats[0].FullName;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerChannel {ChannelId=x.ChannelId,AccessHash=x.AccessHash };
                await FetchMessages(peer, inputPeer);
                channelItem = new TLPeerChannel { Id = channel.ChannelId };
                ChatType = 2;
            }
            else if (chat != null)
            {
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                DialogTitle = chatDetails.Value.Chats[0].FullName;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerChat { ChatId = chat.ChatId, AccessHash = chat.AccessHash };
                await FetchMessages(peer, inputPeer);
                chatItem = new TLPeerChat { Id = chat.ChatId };
                ChatType = 1;
            }
        }


        public RelayCommand SendCommand => new RelayCommand(SendMessage);
        private async void SendMessage()
        {
            var messageText = SendTextHolder;

            TLPeerBase toId = null;

            switch (ChatType)
            {
                case 0:
                    toId = new TLPeerUser { Id = int.Parse(Item.Id.ToString()) };
                    break;
                case 1:
                    toId = new TLPeerChat { Id = int.Parse(chatItem.Id.ToString()) };
                    break;
                case 2:
                    toId = new TLPeerChannel { Id = int.Parse(channelItem.Id.ToString()) };
                    break;
            }

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);

            ListX.Insert(ListX.Count, message);

            CacheService.SyncSendingMessage(message, null, toId, async (m) =>
            {
                await ProtoService.SendMessageAsync(message);
            });
        }
    }
}
