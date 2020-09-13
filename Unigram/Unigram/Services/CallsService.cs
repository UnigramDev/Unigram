using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services.Updates;
using Unigram.Services.ViewService;
using Unigram.ViewModels;
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Services
{
    public interface IVoipService : IHandle<UpdateCall>, IHandle<UpdateNewCallSignalingData>
    {
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }
        string CurrentVideoInput { get; set; }

        VoipManager Manager { get; }
        VoipVideoCapture Capturer { get; set; }

        Call Call { get; }
        DateTime CallStarted { get; }

        void Show();

        void Start(long chatId, bool video);

        CallProtocol GetProtocol();
    }

    public class VoipService : TLViewModelBase, IVoipService
    {
        private readonly IViewService _viewService;

        private readonly MediaPlayer _mediaPlayer;

        private Call _call;
        private DateTime _callStarted;
        private VoipManager _controller;
        private VoipVideoCapture _capturer;

        private VoIPPage _callPage;
        private OverlayPage _callDialog;
        private AppWindow _callLifetime;

        public VoipService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _viewService = viewService;

            if (ApiInfo.IsMediaSupported)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.CommandManager.IsEnabled = false;
                _mediaPlayer.AudioDeviceType = MediaPlayerAudioDeviceType.Communications;
                _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Communications;
            }

            aggregator.Subscribe(this);
        }

        public CallProtocol GetProtocol()
        {
            return new CallProtocol(true, true, 92, 92, new[] { "3.0.0" });
        }

        public VoipManager Manager => _controller;
        public VoipVideoCapture Capturer
        {
            get => _capturer;
            set => _capturer = value;
        }

        public async void Start(long chatId, bool video)
        {
            var chat = CacheService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var call = Call;
            if (call != null)
            {
                var callUser = CacheService.GetUser(call.UserId);
                if (callUser != null && callUser.Id != user.Id)
                {
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.VoipOngoingAlert, callUser.GetFullName(), user.GetFullName()), Strings.Resources.VoipOngoingAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {

                    }
                }
                else
                {
                    Show();
                }

                return;
            }

            var fullInfo = CacheService.GetUserFull(user.Id);
            if (fullInfo != null && fullInfo.HasPrivateCalls)
            {
                await MessagePopup.ShowAsync(string.Format(Strings.Resources.CallNotAvailable, user.GetFullName()), Strings.Resources.VoipFailed, Strings.Resources.OK);
                return;
            }

            var protocol = GetProtocol();

            var response = await ProtoService.SendAsync(new CreateCall(user.Id, protocol, video));
            if (response is Error error)
            {
                if (error.Code == 400 && error.Message.Equals("PARTICIPANT_VERSION_OUTDATED"))
                {
                    var message = video
                        ? Strings.Resources.VoipPeerVideoOutdated
                        : Strings.Resources.VoipPeerOutdated;
                    await MessagePopup.ShowAsync(string.Format(message, user.FirstName), Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Code == 400 && error.Message.Equals("USER_PRIVACY_RESTRICTED"))
                {
                    await MessagePopup.ShowAsync(string.Format(Strings.Resources.CallNotAvailable, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        public string CurrentAudioInput
        {
            get
            {
                return SettingsService.Current.VoIP.InputDevice;
            }
            set
            {
                SettingsService.Current.VoIP.InputDevice = value;

                if (_controller != null)
                {
                    _controller.SetAudioInputDevice(value);
                }
            }
        }

        public string CurrentVideoInput
        {
            get
            {
                return SettingsService.Current.VoIP.VideoDevice;
            }
            set
            {
                SettingsService.Current.VoIP.VideoDevice = value;

                if (_capturer != null)
                {
                    _capturer.SwitchToDevice(value);
                }
            }
        }

        public string CurrentAudioOutput
        {
            get
            {
                return SettingsService.Current.VoIP.OutputDevice;
            }
            set
            {
                SettingsService.Current.VoIP.OutputDevice = value;

                if (_controller != null)
                {
                    _controller.SetAudioOutputDevice(value);
                }
            }
        }

        public void Handle(UpdateNewCallSignalingData update)
        {
            if (_controller != null)
            {
                _controller.ReceiveSignalingData(update.Data);
            }
        }

        public async void Handle(UpdateCall update)
        {
            if (_call != null && _call.Id != update.Call.Id)
            {
                return;
            }

            _call = update.Call;

            if (update.Call.State is CallStatePending pending)
            {
                if (update.Call.IsOutgoing && pending.IsCreated && pending.IsReceived)
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_ringback.mp3"));
                    _mediaPlayer.IsLoopingEnabled = true;
                    _mediaPlayer.Play();
                }
            }
            else if (update.Call.State is CallStateExchangingKeys exchangingKeys)
            {
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_connecting.mp3"));
                _mediaPlayer.IsLoopingEnabled = false;
                _mediaPlayer.Play();
            }
            else if (update.Call.State is CallStateReady ready)
            {
                var user = CacheService.GetUser(update.Call.UserId);
                if (user == null)
                {
                    return;
                }

                //VoIPControllerWrapper.UpdateServerConfig(ready.Config);

                var logFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", $"voip{update.Call.Id}.txt");
                var statsDumpFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.statsDump.txt");

                var call_packet_timeout_ms = CacheService.Options.CallPacketTimeoutMs;
                var call_connect_timeout_ms = CacheService.Options.CallConnectTimeoutMs;

                if (_controller != null)
                {
                    //_controller.Dispose();
                    _controller = null;
                }

                //var config = new VoIPConfig
                //{
                //    initTimeout = call_packet_timeout_ms / 1000.0,
                //    recvTimeout = call_connect_timeout_ms / 1000.0,
                //    dataSaving = base.Settings.UseLessData,
                //    enableAEC = true,
                //    enableNS = true,
                //    enableAGC = true,

                //    enableVolumeControl = true,

                //    logFilePath = logFile,
                //    statsDumpFilePath = statsDumpFile
                //};

                var servers = new List<VoipServer>();

                foreach (var server in ready.Servers)
                {
                    if (server.Type is CallServerTypeWebrtc webRtc)
                    {
                        servers.Add(new VoipServer
                        {
                            Host = server.IpAddress,
                            Port = (ushort)server.Port,
                            Login = webRtc.Username,
                            Password = webRtc.Password,
                            IsTurn = webRtc.SupportsTurn
                        });
                    }
                }

                _capturer = update.Call.IsVideo
                    ? new VoipVideoCapture(SettingsService.Current.VoIP.VideoDevice)
                    : null;

                var descriptor = new VoipDescriptor
                {
                    InitializationTimeout = call_packet_timeout_ms / 1000.0,
                    ReceiveTimeout = call_connect_timeout_ms / 1000.0,
                    Servers = servers,
                    EncryptionKey = ready.EncryptionKey,
                    IsOutgoing = update.Call.IsOutgoing,
                    VideoCapture = _capturer,
                    AudioInputId = SettingsService.Current.VoIP.InputDevice,
                    AudioOutputId = SettingsService.Current.VoIP.OutputDevice
                };

                _controller = new VoipManager(descriptor);
                _controller.StateUpdated += OnStateUpdated;
                _controller.SignalingDataEmitted += OnSignalingDataEmitted;

                BeginOnUIThread(() =>
                {
                    Show(update.Call, _controller, _capturer, _callStarted);
                });

                _controller.Start();
            }
            else if (update.Call.State is CallStateDiscarded discarded)
            {
                if (discarded.NeedDebugInformation)
                {
                    //ProtoService.Send(new SendCallDebugInformation(update.Call.Id, _controller.GetDebugLog()));
                }

                if (discarded.NeedRating)
                {
                    BeginOnUIThread(async () => await SendRatingAsync(update.Call.Id));
                }

                if (_controller != null)
                {
                    _controller.StateUpdated -= OnStateUpdated;
                    _controller.SignalingDataEmitted -= OnSignalingDataEmitted;

                    _controller.SetIncomingVideoOutput(null);
                    _controller.Dispose();
                    _controller = null;
                }

                if (_capturer != null)
                {
                    _capturer.SetOutput(null);
                    _capturer.Dispose();
                    _capturer = null;
                }

                _call = null;
            }
            else if (update.Call.State is CallStateError error)
            {
                if (string.Equals(error.Error.Message, "PARTICIPANT_VERSION_OUTDATED", StringComparison.OrdinalIgnoreCase))
                {
                    var user = CacheService.GetUser(update.Call.UserId);
                    if (user == null)
                    {
                        return;
                    }

                    var message = update.Call.IsVideo
                        ? Strings.Resources.VoipPeerVideoOutdated
                        : Strings.Resources.VoipPeerOutdated;
                    BeginOnUIThread(async () => await MessagePopup.ShowAsync(string.Format(message, user.FirstName), Strings.Resources.AppName, Strings.Resources.OK));
                }
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

                            Show(update.Call, null, null, _callStarted);
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
                        Show(update.Call, _controller, _capturer, _callStarted);
                        break;
                }
            });
        }

        private void OnStateUpdated(VoipManager sender, VoipState args)
        {
            BeginOnUIThread(() =>
            {
                if (args == VoipState.WaitInit || args == VoipState.WaitInitAck)
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/voip_connecting.mp3"));
                    _mediaPlayer.IsLoopingEnabled = false;
                    _mediaPlayer.Play();
                }
                else if (args == VoipState.Established)
                {
                    _callStarted = DateTime.Now;
                    _mediaPlayer.Source = null;
                }
            });
        }

        private void OnSignalingDataEmitted(VoipManager sender, SignalingDataEmittedEventArgs args)
        {
            var call = _call;
            if (call != null)
            {
                ProtoService.Send(new SendCallSignalingData(_call.Id, args.Data));
            }
            else
            {
                if (_controller != null)
                {
                    _controller.StateUpdated -= OnStateUpdated;
                    _controller.SignalingDataEmitted -= OnSignalingDataEmitted;

                    _controller.SetIncomingVideoOutput(null);
                    _controller.Dispose();
                    _controller = null;
                }

                if (_capturer != null)
                {
                    _capturer.SetOutput(null);
                    _capturer.Dispose();
                    _capturer = null;
                }
            }
        }

        private async Task SendRatingAsync(int callId)
        {
            var dialog = new CallRatingPopup();

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                // We need updates here
                //await ProtoService.SendAsync(new SendCallRating(callId, dialog.Rating, dialog.Rating >= 1 && dialog.Rating <= 4 ? dialog.Comment : string.Empty));

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

                    ProtoService.Send(new SendMessage(chat.Id, 0, new MessageSendOptions(false, false, null), null, new InputMessageDocument(new InputFileLocal(file.Path), null, false, null)));
                }
            }
        }

        public Call Call => _call;
        public DateTime CallStarted => _callStarted;

        public async void Show()
        {
            if (_call == null)
            {
                return;
            }

            Show(_call, _controller, _capturer, _callStarted);

            if (_callDialog != null)
            {
                _callDialog.IsOpen = true;
            }
            //else if (_callLifetime != null)
            //{
            //    _callLifetime = await _viewService.OpenAsync(() => _callPage = _callPage ?? new VoIPPage(ProtoService, CacheService, Aggregator, _call, _controller, _capturer, _callStarted), _call.Id);
            //    _callLifetime.WindowWrapper.ApplicationView().Consolidated -= ApplicationView_Consolidated;
            //    _callLifetime.WindowWrapper.ApplicationView().Consolidated += ApplicationView_Consolidated;
            //}

            Aggregator.Publish(new UpdateCallDialog(_call, true));
        }

        private async void Show(Call call, VoipManager controller, VoipVideoCapture capturer, DateTime started)
        {
            if (_callPage == null)
            {
                if (ApiInformation.IsTypePresent("Windows.UI.WindowManagement.AppWindow"))
                {
                    var window = _callLifetime;
                    if (window == null)
                    {
                        _callPage = new VoIPPage(ProtoService, CacheService, Aggregator, this);

                        window = await AppWindow.TryCreateAsync();
                        window.PersistedStateId = "Calls";
                        //window.TitleBar.ExtendsContentIntoTitleBar = true;
                        window.TitleBar.BackgroundColor = Color.FromArgb(0xFF, 0x17, 0x17, 0x17);
                        window.TitleBar.ButtonBackgroundColor = Color.FromArgb(0xFF, 0x17, 0x17, 0x17);
                        window.TitleBar.ForegroundColor = Colors.White;
                        window.TitleBar.ButtonForegroundColor = Colors.White;
                        window.Closed += (s, args) =>
                        {
                            if (_callPage != null)
                            {
                                _callPage.Dispose();
                                _callPage = null;
                            }

                            Aggregator.Publish(new UpdateCallDialog(_call, false));

                            _callLifetime = null;
                            window = null;
                        };

                        _callLifetime = window;
                        ElementCompositionPreview.SetAppWindowContent(window, _callPage);
                    }

                    var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

                    await window.TryShowAsync();
                    window.RequestSize(new Windows.Foundation.Size(720 * scaleFactor, 540 * scaleFactor));
                    //window.RequestMoveAdjacentToCurrentView();
                }
                else
                {
                    _callPage = new VoIPPage(ProtoService, CacheService, Aggregator, this);

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

                _callPage.Connect(capturer);
                _callPage.Update(call, started);
            });
        }

        private async void Hide()
        {
            if (_callPage != null)
            {
                await _callPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (_callDialog != null)
                    {
                        _callDialog.IsOpen = false;
                        _callDialog = null;
                    }
                    else if (_callLifetime != null)
                    {
                        await _callLifetime.CloseAsync();
                        _callLifetime = null;
                    }

                    if (_callPage != null)
                    {
                        _callPage.Dispose();
                        _callPage = null;
                    }
                });

                Aggregator.Publish(new UpdateCallDialog(_call, true));
            }
        }
    }
}
