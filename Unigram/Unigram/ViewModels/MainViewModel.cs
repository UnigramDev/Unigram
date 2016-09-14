using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase
    {
        private readonly IPushService _pushService;

        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPushService pushService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;

            Dialogs = new DialogCollection(protoService, cacheService);
            aggregator.Subscribe(Dialogs);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await _pushService.RegisterAsync();
        }

        public DialogCollection Dialogs { get; private set; }
    }
}
