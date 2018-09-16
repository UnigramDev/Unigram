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
        private VoIPControllerWrapper _controller;

        private PhoneCallPage _callPage;
        private ContentDialogBase _callDialog;
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

        public void Handle(UpdateCall update)
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

                var logFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.logFile.txt");
                var statsDumpFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.statsDump.txt");

                var call_packet_timeout_ms = CacheService.GetOption<OptionValueInteger>("call_packet_timeout_ms");
                var call_connect_timeout_ms = CacheService.GetOption<OptionValueInteger>("call_connect_timeout_ms");

                if (_controller != null)
                {
                    _controller.Dispose();
                    _controller = null;
                }

                _controller = new VoIPControllerWrapper();
                _controller.SetConfig(call_packet_timeout_ms.Value / 1000.0, call_connect_timeout_ms.Value / 1000.0, base.Settings.UseLessData, true, true, true, logFile, statsDumpFile);

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
                            _mediaPlayer.Source = null;
                        }
                    });
                };

                BeginOnUIThread(() =>
                {
                    Show(update.Call, _controller);
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
                    BeginOnUIThread(async () =>
                    {
                        var dialog = new PhoneCallRatingView();
                        var confirm = await dialog.ShowQueuedAsync();
                        if (confirm == ContentDialogResult.Primary)
                        {
                            ProtoService.Send(new SendCallRating(update.Call.Id, dialog.Rating + 1, dialog.Rating >= 0 && dialog.Rating <= 3 ? dialog.Comment : null));
                        }
                    });
                }

                _controller?.Dispose();
                _controller = null;
                _call = null;
            }

            BeginOnUIThread(() =>
            {
                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                        if (update.Call.IsOutgoing && discarded.Reason is CallDiscardReasonDeclined)
                        {
                            _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_busy.mp3"));
                            _mediaPlayer.IsLoopingEnabled = true;
                            _mediaPlayer.Play();

                            Show(update.Call, null);
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
                        Show(update.Call, null);
                        break;
                }
            });
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
            Show(_call, _controller);

            if (_callDialog != null)
            {
                _callDialog.IsOpen = true;
            }
            else if (_callLifetime != null)
            {
                _callLifetime = await _viewService.OpenAsync(() => _callPage = _callPage ?? new PhoneCallPage(ProtoService, CacheService, Aggregator, _call, _controller), 0);
            }
        }

        private async void Show(Call call, VoIPControllerWrapper controller)
        {
            if (_callPage == null)
            {
                if (ApiInformation.IsMethodPresent("Windows.UI.ViewManagement.ApplicationView", "IsViewModeSupported") && ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
                {
                    _callLifetime = await _viewService.OpenAsync(() => _callPage = _callPage ?? new PhoneCallPage(ProtoService, CacheService, Aggregator, _call, _controller), 0);
                    _callLifetime.Released += (s, args) =>
                    {
                        _callPage.Dispose();
                        _callPage = null;
                    };
                }
                else
                {
                    _callPage = new PhoneCallPage(ProtoService, CacheService, Aggregator, _call, _controller);

                    _callDialog = new ContentDialogBase();
                    _callDialog.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _callDialog.VerticalAlignment = VerticalAlignment.Stretch;
                    _callDialog.Content = _callPage;
                    _callDialog.IsOpen = true;
                }
            }

            _callPage.BeginOnUIThread(() =>
            {
                if (controller != null)
                {
                    _callPage.Connect(controller);
                }

                _callPage.Update(call);
            });
        }

        private void Hide()
        {
            if (_callPage != null)
            {
                _callPage.BeginOnUIThread(() =>
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
            }
        }
    }
}
