using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Services;
using Unigram.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;

namespace Unigram.Common
{
    public class VoIPConnection
    {
        private static VoIPConnection _current;
        public static VoIPConnection Current
        {
            get
            {
                if (_current == null)
                    _current = new VoIPConnection();

                return _current;
            }
        }

        private AppServiceConnection _appConnection;

        public bool IsConnected { get; private set; }

        public async Task<bool> ConnectAsync()
        {
            if (!IsConnected)
            {
                _appConnection = new AppServiceConnection
                {
                    AppServiceName = nameof(VoIPServiceTask),
                    PackageFamilyName = Package.Current.Id.FamilyName
                };

                _appConnection.ServiceClosed += OnServiceClosed;
                _appConnection.RequestReceived += OnRequestReceived;

                var status = await _appConnection.OpenAsync();

                IsConnected = status == AppServiceConnectionStatus.Success;
            }

            return IsConnected;
        }

        public IAsyncOperation<AppServiceResponse> SendUpdateAsync(TLUpdateBase update)
        {
            return _appConnection.SendMessageAsync(new ValueSet { { nameof(update), TLSerializationService.Current.Serialize(update) } });
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            var message = args.Request.Message;

            try
            {
                if (message.ContainsKey("caption") && message.ContainsKey("request"))
                {
                    var caption = message["caption"] as string;
                    var buffer = message["request"] as string;
                    var req = TLSerializationService.Current.Deserialize(buffer) as TLObject;

                    if (caption.Equals("voip.getUser") && req is TLPeerUser userPeer)
                    {
                        var user = InMemoryCacheService.Current.GetUser(userPeer.UserId);
                        if (user != null)
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "result", TLSerializationService.Current.Serialize(user) } });
                        }
                        else
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "error", TLSerializationService.Current.Serialize(new TLRPCError { ErrorMessage = "USER_NOT_FOUND", ErrorCode = 404 }) } });
                        }
                    }
                    else
                    {
                        var response = await MTProtoService.Current.SendRequestAsync<object>(caption, req);
                        if (response.IsSucceeded)
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "result", TLSerializationService.Current.Serialize(response.Result) } });
                        }
                        else
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "error", TLSerializationService.Current.Serialize(response.Error) } });
                        }
                    }
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            IsConnected = false;
            Debug.WriteLine("HubClient.OnServiceClosed()");

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await ConnectAsync());
        }
    }
}
