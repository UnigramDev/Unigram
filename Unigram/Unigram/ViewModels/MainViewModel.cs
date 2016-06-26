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
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase
    {
        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Dialogs = new DialogCollection(protoService, cacheService);
            aggregator.Subscribe(Dialogs);
        }

        bool already = false;

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (already) return;
            already = true;

            var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            if (channel.Uri != SettingsHelper.ChannelUri)
            {
                var result = await ProtoService.RegisterDeviceAsync(8, channel.Uri);
                if (result.IsSucceeded && result.Value)
                {
                    SettingsHelper.ChannelUri = channel.Uri;
                }
            }
        }

        public DialogCollection Dialogs { get; private set; }
    }
}
