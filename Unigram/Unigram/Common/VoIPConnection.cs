using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
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
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
            try
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
            }
            catch
            {
                IsConnected = false;
            }

            return IsConnected;
        }

        public IAsyncOperation<AppServiceResponse> SendUpdateAsync(TLUpdatePhoneCall update)
        {
            Debug.WriteLine("[{0:HH:mm:ss.fff}] Received VoIP update: " + update.PhoneCall, DateTime.Now);

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

        public IAsyncOperation<AppServiceResponse> SendRequestAsync(string caption, TLObject request)
        {
            try
            {
                return _appConnection.SendMessageAsync(new ValueSet { { nameof(caption), caption }, { nameof(request), TLSerializationService.Current.Serialize(request) } });
            }
            catch
            {
                return AsyncInfo.Run(token => Task.FromResult(null as AppServiceResponse));
            }
        }

        private PhoneCallPage _phoneView;
        private bool _phoneViewExists;

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
                            var tupleBase = new TLTuple<int, TLPhoneCallBase, TLUserBase, string>(from);
                            var tuple = new TLTuple<TLPhoneCallState, TLPhoneCallBase, TLUserBase, string>((TLPhoneCallState)tupleBase.Item1, tupleBase.Item2, tupleBase.Item3, tupleBase.Item4);

                            if (tuple.Item2 is TLPhoneCallDiscarded && _phoneView != null)
                            {

                                var newView = _phoneView;
                                _phoneViewExists = false;
                                _phoneView = null;

                                await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    newView.SetCall(tuple);
                                    CoreApplication.GetCurrentView().CoreWindow.Close();
                                });

                                return;
                            }

                            if (_phoneViewExists == false)
                            {
                                _phoneViewExists = true;

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
                                    _phoneView = newPlayer;
                                });

                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    if (ApiInformation.IsMethodPresent("Windows.UI.ViewManagement.ApplicationView", "IsViewModeSupported") && ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
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
                            else if (_phoneView != null)
                            {
                                await _phoneView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    _phoneView.SetCall(tuple);
                                });
                            }
                        }
                    }
                    else if (caption.Equals("voip.setCallRating") && req is TLInputPhoneCall peer)
                    {
                        Execute.BeginOnUIThread(async () =>
                        {
                            var dialog = new PhoneCallRateView();
                            var confirm = await dialog.ShowAsync();
                            if (confirm == ContentDialogResult.Primary)
                            {
                                await MTProtoService.Current.SetCallRatingAsync(peer, dialog.Rating, dialog.Rating >= 0 && dialog.Rating <= 3 ? dialog.Comment : null);
                            }
                        });
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
