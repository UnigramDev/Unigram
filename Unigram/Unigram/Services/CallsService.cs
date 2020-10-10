using Microsoft.Graphics.Canvas;
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
using Windows.Devices.Enumeration;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

        void StartCaptureInternal(GraphicsCaptureItem item);

        CallProtocol GetProtocol();

        Task<bool> CheckAccessAsync(bool video);
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
        private ViewLifetimeControl _callLifetime;

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

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private CanvasDevice _canvasDevice;
        private GraphicsCaptureSession _session;

        public void StartCaptureInternal(GraphicsCaptureItem item)
        {
            _item = item;
            _canvasDevice = new CanvasDevice();

            _framePool = Direct3D11CaptureFramePool.Create(
                _canvasDevice, // D3D device
                DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
                2, // Number of frames
                _item.Size); // Size of the buffers

            _session = _framePool.CreateCaptureSession(_item);
            _session.StartCapture();

            _framePool.FrameArrived += (s, a) =>
            {
                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };
        }

        private async void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            if (Capturer != null)
            {
                //using (var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface))
                {

                    //var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface);
                    //var bytes = bitmap.GetPixelBytes();

                    //Capturer.FeedBytes(frame.ContentSize.Width, frame.ContentSize.Height, bytes.ToArray());
                    var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                    Capturer.FeedBytes(bitmap);
                }
            }
            else
            {
                //_framePool.Dispose();
                //_session.Dispose();
            }
        }

        public async void Start(long chatId, bool video)
        {
            var permissions = await CheckAccessAsync(video);
            if (permissions == false)
            {
                return;
            }

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

        public async Task<bool> CheckAccessAsync(bool video)
        {
            var audioPermission = await CheckDeviceAccessAsync(true, video);
            if (audioPermission == false)
            {
                return false;
            }

            if (video)
            {
                var videoPermission = await CheckDeviceAccessAsync(false, true);
                if (videoPermission == false)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> CheckDeviceAccessAsync(bool audio, bool video)
        {
            var access = DeviceAccessInformation.CreateFromDeviceClass(audio ? DeviceClass.AudioCapture : DeviceClass.VideoCapture);
            if (access.CurrentStatus == DeviceAccessStatus.Unspecified)
            {
                MediaCapture capture = null;
                try
                {
                    capture = new MediaCapture();
                    var settings = new MediaCaptureInitializationSettings();
                    settings.StreamingCaptureMode = video
                        ? StreamingCaptureMode.AudioAndVideo
                        : StreamingCaptureMode.Audio;
                    await capture.InitializeAsync(settings);
                }
                catch { }
                finally
                {
                    if (capture != null)
                    {
                        capture.Dispose();
                        capture = null;
                    }
                }

                return false;
            }
            else if (access.CurrentStatus != DeviceAccessStatus.Allowed)
            {
                var message = audio
                    ? video
                    ? Strings.Resources.PermissionNoAudio
                    : Strings.Resources.PermissionNoAudioVideo
                    : Strings.Resources.PermissionNoCamera;

                this.BeginOnUIThread(async () =>
                {
                    var confirm = await MessagePopup.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.PermissionOpenSettings, Strings.Resources.OK);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                    }
                });

                return false;
            }

            return true;
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

                    ProtoService.Send(new SendMessage(chat.Id, 0, 0, new MessageSendOptions(false, false, null), null, new InputMessageDocument(new InputFileLocal(file.Path), null, false, null)));
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
            else if (_callLifetime != null)
            {
                _callLifetime = await _viewService.OpenAsync(control => _callPage = _callPage ?? new VoIPPage(ProtoService, CacheService, Aggregator, this), _call.Id, 720, 540, ApplicationViewMode.Default);
                _callLifetime.Released -= ApplicationView_Released;
                _callLifetime.Released += ApplicationView_Released;
            }

            Aggregator.Publish(new UpdateCallDialog(_call, true));
        }

        private async void Show(Call call, VoipManager controller, VoipVideoCapture capturer, DateTime started)
        {
            if (_callPage == null)
            {
                if (ApiInformation.IsPropertyPresent("Windows.UI.ViewManagement.ApplicationView", "PersistedStateId"))
                {
                    _callLifetime = await _viewService.OpenAsync(control => _callPage = _callPage ?? new VoIPPage(ProtoService, CacheService, Aggregator, this), call.Id, 720, 540, ApplicationViewMode.Default);
                    _callLifetime.Released -= ApplicationView_Released;
                    _callLifetime.Released += ApplicationView_Released;
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
                await _callPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (_callDialog != null)
                    {
                        _callDialog.IsOpen = false;
                        _callDialog = null;
                    }
                    else if (_callLifetime != null)
                    {
                        _callLifetime.WindowWrapper.Window.Close();
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

        private void ApplicationView_Released(object sender, EventArgs e)
        {
            if (_callLifetime != null)
            {
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
