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
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            user = parameter as TLUser;
            if (user != null)
            {
                Item = user;
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
            var toId = new TLPeerUser { Id = int.Parse(Item.Id.ToString()) };
            var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);
            await protoService.SendMessageAsync(message);
        }
    }
}
