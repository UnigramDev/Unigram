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

namespace Unigram.ViewModels
{

    public class DialogPageViewModel : UnigramViewModelBase
    {
        int ChatType=-1;
        //0 if private, 1 if group, 2 if supergroup/channel
        int date;
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            channel = parameter as TLInputPeerChannel;
            chat = parameter as TLInputPeerChat;
            user = parameter as TLUser;
            if (user != null)
            {
                Item = user;
                ChatType = 0;
            }
            else if (channel != null)
            {
                channelItem = new TLPeerChannel { Id = channel.ChannelId };
                ChatType = 2;
            }
            else if (chat != null)
            {
                chatItem = new TLPeerChat { Id = chat.ChatId };
                ChatType = 1;
            }

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
            //var toId = new TLPeerUser { Id = int.Parse(Item.Id.ToString()) };            
            //   var toId1 = new TLPeerChat { Id=int.Parse(channelItem.Id.ToString())};
            //TLPeerBase toId = null;
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
