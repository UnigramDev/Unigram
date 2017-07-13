using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Networking.PushNotifications;

namespace Unigram.Core.Services
{
    public interface IPushService
    {
        Task RegisterAsync();
        Task UnregisterAsync();
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

                    channel.PushNotificationReceived += OnPushNotificationReceived;
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }
            }
        }

        private void OnPushNotificationReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            if (args.NotificationType == PushNotificationType.Raw)
            {
                if (JsonValue.TryParse(args.RawNotification.Content, out JsonValue node))
                {
                    var notification = node.GetObject();
                    var data = notification.GetNamedObject("data");

                    if (data.ContainsKey("loc_key"))
                    {
                        var muted = data.GetNamedString("mute", "0") == "1";
                        if (muted)
                        {
                            return;
                        }

                        var custom = data.GetNamedObject("custom", null);
                        if (custom == null)
                        {
                            return;
                        }

                        TLPeerBase peer = null;
                        if (custom.ContainsKey("chat_id") && int.TryParse(custom.GetNamedString("chat_id"), out int chat_id))
                        {
                            peer = new TLPeerChat { ChatId = chat_id };
                        }
                        else if (custom.ContainsKey("channel_id") && int.TryParse(custom.GetNamedString("channel_id"), out int channel_id))
                        {
                            peer = new TLPeerChannel { ChannelId = channel_id };
                        }
                        else if (custom.ContainsKey("from_id") && int.TryParse(custom.GetNamedString("from_id"), out int from_id))
                        {
                            peer = new TLPeerUser { UserId = from_id };
                        }
                        else if (custom.ContainsKey("contact_id") && int.TryParse(custom.GetNamedString("contact_id"), out int contact_id))
                        {
                            peer = new TLPeerUser { UserId = contact_id };
                        }

                        if (peer == null)
                        {
                            return;
                        }

                        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                        if (service == null)
                        {
                            return;
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            if (service.Frame.Content is DialogPage page && peer.Equals(service.CurrentPageParam))
                            {
                                if (!page.ViewModel.IsActive || !App.IsActive || !App.IsVisible)
                                {
                                    return;
                                }

                                args.Cancel = true;
                            }
                        });
                    }
                }
            }
        }

        public async Task UnregisterAsync()
        {
            var channel = SettingsHelper.ChannelUri;
            var response = await _protoService.UnregisterDeviceAsync(8, channel);
            if (response.IsSucceeded)
            {
            }

            SettingsHelper.ChannelUri = null;
        }
    }
}
