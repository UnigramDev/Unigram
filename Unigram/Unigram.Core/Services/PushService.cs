using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Unigram.Common;
using Windows.Networking.PushNotifications;

namespace Unigram.Core.Services
{
    public interface IPushService
    {
        Task RegisterAsync();
    }

    public class PushService : IPushService
    {
        private readonly IMTProtoService _protoService;
        private readonly DisposableMutex _registrationLock;
        private bool _alreadyRegistered;

        public PushService(IMTProtoService protoService)
        {
            _protoService = protoService;
            _registrationLock = new DisposableMutex();
        }

        public async Task RegisterAsync()
        {
            using (await _registrationLock.WaitAsync())
            {
                if (_alreadyRegistered) return;
                _alreadyRegistered = true;

                try
                {

                    var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                    if (channel.Uri != SettingsHelper.ChannelUri)
                    {
                        var oldUri = SettingsHelper.ChannelUri;

                        var result = await _protoService.RegisterDeviceAsync(8, channel.Uri);
                        if (result.IsSucceeded)
                        {
                            SettingsHelper.ChannelUri = channel.Uri;
                        }

                        await _protoService.UnregisterDeviceAsync(8, oldUri);
                    }
                }
                catch { }
            }
        }
    }
}
