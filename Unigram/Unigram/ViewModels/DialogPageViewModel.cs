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

    public class DialogPageViewModel : UnigramViewModelBase
    {
        int ChatType=-1;
        //0 if private, 1 if group, 2 if supergroup/channel
        int date;
        public ObservableCollection<string> ListX= new ObservableCollection<string>();
        public string DialogTitle;
        public string debug;
        public DialogPageViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            channel = parameter as TLInputPeerChannel;
            chat = parameter as TLInputPeerChat;
            user = parameter as TLUser;
            if (user != null)
            {
                //Happy Birthday Alexmitter xD
                ListX.Clear();
                Item = user;
                DialogTitle = Item.FullName;
                TLPeerBase peer = new TLPeerUser { Id = SettingsHelper.UserId };
                TLInputPeerBase inputPeer = new TLInputPeerUser { UserId = user.Id };
                var x =await ProtoService.GetHistoryAsync(null, inputPeer, peer, false, 0,int.MaxValue, 10);
                TLVector<TLMessageBase> y = x.Value.Messages;
                string[] yy = new string[10];
                foreach (var item in y)
                {
                    var xy = (TLMessage)item;
                    ListX.Add(xy.Message);
                }
                ChatType = 0;
            }
            else if (channel != null)
            {

                TLInputChannel x=new TLInputChannel();
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var channelDetails = await ProtoService.GetFullChannelAsync(x);
                DialogTitle = channelDetails.Value.Chats[0].FullName;
                channelItem = new TLPeerChannel { Id = channel.ChannelId };
                ChatType = 2;
            }
            else if (chat != null)
            {
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                DialogTitle = chatDetails.Value.Chats[0].FullName;
                chatItem = new TLPeerChat { Id = chat.ChatId };
                ChatType = 1;
            }
            ListX.Reverse();
        }


        public RelayCommand SendCommand => new RelayCommand(SendMessage);
        private async void SendMessage()
        {
            var messageText = SendTextHolder;
            var deviceInfoService = new DeviceInfoService();
            var eventAggregator = new TelegramEventAggregator();
            var cacheService = new InMemoryCacheService(eventAggregator);
            var updatesService = new UpdatesService(cacheService, eventAggregator);
            var transportService = new TransportService();
            var connectionService = new ConnectionService(deviceInfoService);
            var manualResetEvent = new ManualResetEvent(false);
            var protoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService);
            date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
            TLMessage message = new TLMessage();
            switch (ChatType)
            {               
                case 0:
                    message = TLUtils.GetMessage(SettingsHelper.UserId, new TLPeerUser { Id = int.Parse(Item.Id.ToString()) }, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);
                    break;
                case 1:
                    message = TLUtils.GetMessage(SettingsHelper.UserId, new TLPeerChat { Id = int.Parse(chatItem.Id.ToString()) }, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);
                    break;
                case 2:
                     message = TLUtils.GetMessage(SettingsHelper.UserId, new TLPeerChannel { Id = int.Parse(channelItem.Id.ToString()) }, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);
                    break;
            }
            await protoService.SendMessageAsync(message);
            
        }
    }
}
