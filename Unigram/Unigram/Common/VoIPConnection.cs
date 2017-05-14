using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Controls.Views;
using Unigram.Core.Services;
using Unigram.Tasks;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

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
            try
            {
                return _appConnection.SendMessageAsync(new ValueSet { { nameof(update), TLSerializationService.Current.Serialize(update) } });
            }
            catch
            {
                return AsyncInfo.Run(token => Task.FromResult(null as AppServiceResponse));
            }
        }

        public IAsyncOperation<AppServiceResponse> SendRequestAsync(string caption)
        {
            try
            {
                return _appConnection.SendMessageAsync(new ValueSet { { nameof(caption), caption } });
            }
            catch
            {
                return AsyncInfo.Run(token => Task.FromResult(null as AppServiceResponse));
            }
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
                    var req = TLSerializationService.Current.Deserialize(buffer);

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
                    else if (caption.Equals("voip.getConfig"))
                    {
                        var config = InMemoryCacheService.Current.GetConfig();
                        await args.Request.SendResponseAsync(new ValueSet { { "result", TLSerializationService.Current.Serialize(config) } });
                    }
                    else if (caption.Equals("voip.callInfo") && req is byte[] data)
                    {
                        using (var from = new TLBinaryReader(data))
                        {
                            var tuple = new TLTuple<TLPhoneCallBase, TLUserBase, string>(from);

                            PhoneCallPage newPlayer = null;
                            CoreApplicationView newView = CoreApplication.CreateNewView();
                            var newViewId = 0;
                            await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                newPlayer = new PhoneCallPage();
                                Window.Current.Content = newPlayer;
                                Window.Current.Activate();
                                newViewId = ApplicationView.GetForCurrentView().Id;

                                newPlayer.SetCall(tuple);
                            });

                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                var overlay = ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay);
                                if (overlay)
                                {
                                    var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                                    preferences.CustomSize = new Size(340, 200);

                                    var viewShown = await ApplicationViewSwitcher.TryShowAsViewModeAsync(newViewId, ApplicationViewMode.CompactOverlay, preferences);
                                }
                                else
                                {
                                    //await ApplicationViewSwitcher.SwitchAsync(newViewId);
                                    await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
                                }
                            });
                        }
                    }
                    else
                    {
                        var response = await MTProtoService.Current.SendRequestAsync<object>(caption, req as TLObject);
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
            Debug.WriteLine("VoIPConnection.OnServiceClosed()");

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await ConnectAsync());
        }
    }
}
