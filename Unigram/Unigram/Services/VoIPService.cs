using libtgvoip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Services.ViewService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IVoIPService : IHandle<UpdateCall>
    {
        Call ActiveCall { get; }

        void Show();
    }

    public class VoIPService : TLViewModelBase, IVoIPService
    {
        private readonly IViewService _viewService;

        private readonly MediaPlayer _mediaPlayer;

        private Call _call;
        private DateTime _callStarted;
        private VoIPControllerWrapper _controller;

        private VoIPPage _callPage;
        private OverlayPage _callDialog;
        private ViewLifetimeControl _callLifetime;

        public VoIPService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _viewService = viewService;

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;
            _mediaPlayer.AudioDeviceType = MediaPlayerAudioDeviceType.Communications;
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Communications;

            aggregator.Subscribe(this);
        }

        public async void Handle(UpdateCall update)
        {
            _call = update.Call;

            if (update.Call.State is CallStatePending pending)
            {
                if (update.Call.IsOutgoing && pending.IsCreated && pending.IsReceived)
                {
                    if (pending.IsCreated && pending.IsReceived)
                    {
                        _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_ringback.mp3"));
                        _mediaPlayer.IsLoopingEnabled = true;
                        _mediaPlayer.Play();
                    }
                }
            }
            if (update.Call.State is CallStateReady ready)
            {
                var user = CacheService.GetUser(update.Call.UserId);
                if (user == null)
                {
                    return;
                }

                VoIPControllerWrapper.UpdateServerConfig(ready.Config);

                var logFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", $"voip{update.Call.Id}.txt");
                var statsDumpFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.statsDump.txt");

                var call_packet_timeout_ms = CacheService.Options.CallPacketTimeoutMs;
                var call_connect_timeout_ms = CacheService.Options.CallConnectTimeoutMs;

                if (_controller != null)
                {
                    _controller.Dispose();
                    _controller = null;
                }

                _controller = new VoIPControllerWrapper();
                _controller.SetConfig(call_packet_timeout_ms / 1000.0, call_connect_timeout_ms / 1000.0, base.Settings.UseLessData, true, true, true, logFile, statsDumpFile);

                _controller.CallStateChanged += (s, args) =>
                {
                    BeginOnUIThread(() =>
                    {
                        if (args == libtgvoip.CallState.WaitInit || args == libtgvoip.CallState.WaitInitAck)
                        {
                            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_connecting.mp3"));
                            _mediaPlayer.IsLoopingEnabled = false;
                            _mediaPlayer.Play();
                        }
                        else if (args == libtgvoip.CallState.Established)
                        {
                            _callStarted = DateTime.Now;
                            _mediaPlayer.Source = null;
                        }
                    });
                };

                BeginOnUIThread(() =>
                {
                    Show(update.Call, _controller, _callStarted);
                });

                var p2p = base.Settings.PeerToPeerMode == 0 || (base.Settings.PeerToPeerMode == 1 && user.OutgoingLink is LinkStateIsContact);
                var endpoints = new Endpoint[ready.Connections.Count];

                for (int i = 0; i < endpoints.Length; i++)
                {
                    endpoints[i] = new Endpoint { id = ready.Connections[i].Id, ipv4 = ready.Connections[i].Ip, ipv6 = ready.Connections[i].Ipv6, peerTag = ready.Connections[i].PeerTag.ToArray(), port = (ushort)ready.Connections[i].Port };
                }

                _controller.SetEncryptionKey(ready.EncryptionKey.ToArray(), update.Call.IsOutgoing);
                _controller.SetPublicEndpoints(endpoints, ready.Protocol.UdpP2p && p2p, 74);
                _controller.Start();
                _controller.Connect();
            }
            else if (update.Call.State is CallStateDiscarded discarded)
            {
                if (discarded.NeedDebugInformation)
                {
                    ProtoService.Send(new SendCallDebugInformation(update.Call.Id, _controller.GetDebugLog()));
                }

                if (discarded.NeedRating)
                {
                    BeginOnUIThread(async () => await SendRatingAsync(update.Call.Id));
                }

                _controller?.Dispose();
                _controller = null;
                _call = null;
            }

            await Dispatcher.DispatchAsync(() =>
            {
                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                        if (update.Call.IsOutgoing && discarded.Reason is CallDiscardReasonDeclined)
                        {
                            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_busy.mp3"));
                            _mediaPlayer.IsLoopingEnabled = true;
                            _mediaPlayer.Play();

                            Show(update.Call, null, _callStarted);
                        }
                        else
                        {
                            _mediaPlayer.Source = null;

                            Hide();
                        }
                        break;
                    case CallStateError error:
                        Hide();
                        break;
                    default:
                        Show(update.Call, null, _callStarted);
                        break;
                }
            });
        }

        private async Task SendRatingAsync(int callId)
        {
            var dialog = new CallRatingView();
            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                // We need updates here
                await ProtoService.SendAsync(new SendCallRating(callId, dialog.Rating, dialog.Rating >= 1 && dialog.Rating <= 4 ? dialog.Comment : string.Empty));

                if (dialog.IncludeDebugLogs && dialog.Rating <= 3)
                {
                    var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(Path.Combine($"{SessionId}", $"voip{callId}.txt")) as StorageFile;
                    if (file == null)
                    {
                        return;
                    }

                    var chat = await ProtoService.SendAsync(new CreatePrivateChat(4244000, false)) as Chat;
                    if (chat == null)
                    {
                        return;
                    }

                    ProtoService.Send(new SendMessage(chat.Id, 0, false, false, null, new InputMessageDocument(new InputFileLocal(file.Path), null, null)));
                }
            }
        }

        public Call ActiveCall
        {
            get
            {
                return _call;
            }
        }

        public async void Show()
        {
            if (_call == null)
            {
                return;
            }

            Show(_call, _controller, _callStarted);

            if (_callDialog != null)
            {
                _callDialog.IsOpen = true;
            }
            else if (_callLifetime != null)
            {
                _callLifetime = await _viewService.OpenAsync(() => _callPage = _callPage ?? new VoIPPage(ProtoService, CacheService, Aggregator, _call, _controller, _callStarted), _call.Id);
                _callLifetime.WindowWrapper.ApplicationView().Consolidated -= ApplicationView_Consolidated;
                _callLifetime.WindowWrapper.ApplicationView().Consolidated += ApplicationView_Consolidated;
            }

            Aggregator.Publish(new UpdateCallDialog(_call, true));
        }

        private async void Show(Call call, VoIPControllerWrapper controller, DateTime started)
        {
            if (_callPage == null)
            {
                if (ApiInformation.IsMethodPresent("Windows.UI.ViewManagement.ApplicationView", "IsViewModeSupported") && ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
                {
                    _callLifetime = await _viewService.OpenAsync(() => _callPage = _callPage ?? new VoIPPage(ProtoService, CacheService, Aggregator, _call, _controller, _callStarted), call.Id);
                    _callLifetime.WindowWrapper.ApplicationView().Consolidated -= ApplicationView_Consolidated;
                    _callLifetime.WindowWrapper.ApplicationView().Consolidated += ApplicationView_Consolidated;
                }
                else
                {
                    _callPage = new VoIPPage(ProtoService, CacheService, Aggregator, _call, _controller, _callStarted);

                    _callDialog = new OverlayPage();
                    _callDialog.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _callDialog.VerticalAlignment = VerticalAlignment.Stretch;
                    _callDialog.Content = _callPage;
                    _callDialog.IsOpen = true;
                }

                Aggregator.Publish(new UpdateCallDialog(call, true));
            }

            await _callPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (controller != null)
                {
                    _callPage.Connect(controller);
                }

                _callPage.Update(call, started);
            });
        }

        private async void Hide()
        {
            if (_callPage != null)
            {
                await _callPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (_callDialog != null)
                    {
                        _callDialog.IsOpen = false;
                        _callDialog = null;
                    }
                    else if (_callLifetime != null)
                    {
                        _callLifetime.StopViewInUse();
                        _callLifetime.WindowWrapper.Window.Close();
                        _callLifetime = null;
                    }

                    _callPage.Dispose();
                    _callPage = null;
                });

                Aggregator.Publish(new UpdateCallDialog(_call, true));
            }
        }

        private void ApplicationView_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            if (_callLifetime != null)
            {
                _callLifetime.StopViewInUse();
                _callLifetime.WindowWrapper.Window.Close();
                _callLifetime = null;
            }

            if (_callPage != null)
            {
                _callPage.Dispose();
                _callPage = null;
            }

            Aggregator.Publish(new UpdateCallDialog(_call, false));
        }
    }
}
