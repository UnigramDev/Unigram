using System;
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
using Windows.Graphics.Capture;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public enum VoipCaptureType
    {
        None,
        Video,
        Screencast
    }

    public interface IVoipService : IHandle<UpdateCall>, IHandle<UpdateNewCallSignalingData>
    {
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }
        string CurrentVideoInput { get; set; }

        VoipManager Manager { get; }
        IVoipVideoCapture Capturer { get; set; }

        Call Call { get; }
        DateTime CallStarted { get; }

        void Show();

        void Start(long chatId, bool video);

        CallProtocol GetProtocol();

        VoipCaptureType CaptureType { get; }
        Task<IVoipVideoCapture> ToggleCapturingAsync(VoipCaptureType type);
    }

    public class VoipService : TLViewModelBase, IVoipService
    {
        private readonly IViewService _viewService;

        private readonly MediaDeviceWatcher _videoWatcher;

        private readonly MediaDeviceWatcher _inputWatcher;
        private readonly MediaDeviceWatcher _outputWatcher;

        private readonly DisposableMutex _updateLock = new DisposableMutex();

        private Call _call;
        private DateTime _callStarted;
        private VoipManager _manager;
        private IVoipVideoCapture _capturer;

        private VoIPPage _callPage;
        private ViewLifetimeControl _callLifetime;

        public VoipService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _viewService = viewService;

            _videoWatcher = new MediaDeviceWatcher(DeviceClass.VideoCapture, id => SetVideoInputDevice(id));
            _inputWatcher = new MediaDeviceWatcher(DeviceClass.AudioCapture, id => _manager?.SetAudioInputDevice(id));
            _outputWatcher = new MediaDeviceWatcher(DeviceClass.AudioRender, id => _manager?.SetAudioOutputDevice(id));

            aggregator.Subscribe(this);
        }

        private void SetVideoInputDevice(string id)
        {
            if (_capturer is VoipVideoCapture capturer)
            {
                capturer.SwitchToDevice(id);
            }
        }

        public CallProtocol GetProtocol()
        {
            return new CallProtocol(true, true, 92, 92, new[] { "3.0.0" });
        }

        public async Task<IVoipVideoCapture> ToggleCapturingAsync(VoipCaptureType type)
        {
            void Disable()
            {
                if (_capturer != null)
                {
                    _capturer.SetOutput(null);
                    _manager.SetVideoCapture(null);

                    _capturer.Dispose();
                    _capturer = null;
                }
            }

            if (type == VoipCaptureType.None)
            {
                Disable();
            }
            else if (type == VoipCaptureType.Video && _capturer is not VoipVideoCapture)
            {
                Disable();

                if (_manager == null)
                {
                    return null;
                }

                _capturer = new VoipVideoCapture(await _videoWatcher.GetAndUpdateAsync());
                _manager?.SetVideoCapture(_capturer);
            }
            else if (type == VoipCaptureType.Screencast && _capturer is not VoipScreenCapture)
            {
                Disable();

                if (_manager == null || !GraphicsCaptureSession.IsSupported())
                {
                    return null;
                }

                var picker = new GraphicsCapturePicker();
                var item = await picker.PickSingleItemAsync();

                if (item == null || _manager == null)
                {
                    return null;
                }

                _capturer = new VoipScreenCapture(item);
                _manager?.SetVideoCapture(_capturer);
            }

            return _capturer;
        }

        public VoipManager Manager => _manager;
        public IVoipVideoCapture Capturer
        {
            get => _capturer;
            set => _capturer = value;
        }

        public VoipCaptureType CaptureType => _capturer is VoipVideoCapture
            ? VoipCaptureType.Video
            : _capturer is VoipScreenCapture
            ? VoipCaptureType.Screencast
            : VoipCaptureType.None;

        public async void Start(long chatId, bool video)
        {
            var permissions = await MediaDeviceWatcher.CheckAccessAsync(video);
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

        public string CurrentVideoInput
        {
            get => _videoWatcher.Get();
            set => _videoWatcher.Set(value);
        }

        public string CurrentAudioInput
        {
            get => _inputWatcher.Get();
            set => _inputWatcher.Set(value);
        }

        public string CurrentAudioOutput
        {
            get => _outputWatcher.Get();
            set => _outputWatcher.Set(value);
        }

        public void Handle(UpdateNewCallSignalingData update)
        {
            if (_manager != null)
            {
                _manager.ReceiveSignalingData(update.Data);
            }
        }

        public async void Handle(UpdateCall update)
        {
            using (await _updateLock.WaitAsync())
            {
                if (_call != null && _call.Id != update.Call.Id)
                {
                    return;
                }
                else if (_call == null && !update.Call.IsValidState())
                {
                    return;
                }

                _call = update.Call;

                if (update.Call.State is CallStatePending pending)
                {
                    if (pending.IsCreated && pending.IsReceived)
                    {
                        SoundEffects.Play(update.Call.IsOutgoing ? SoundEffect.VoipRingback : SoundEffect.VoipIncoming);
                    }
                }
                else if (update.Call.State is CallStateExchangingKeys exchangingKeys)
                {
                    SoundEffects.Play(SoundEffect.VoipConnecting);
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

                    if (_manager != null)
                    {
                        //_controller.Dispose();
                        _manager = null;
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

                    _capturer = update.Call.IsVideo
                        ? new VoipVideoCapture(await _videoWatcher.GetAndUpdateAsync())
                        : null;

                    var descriptor = new VoipDescriptor
                    {
                        InitializationTimeout = call_packet_timeout_ms / 1000.0,
                        ReceiveTimeout = call_connect_timeout_ms / 1000.0,
                        Servers = ready.Servers,
                        EncryptionKey = ready.EncryptionKey,
                        IsOutgoing = update.Call.IsOutgoing,
                        EnableP2p = ready.Protocol.UdpP2p && ready.AllowP2p,
                        VideoCapture = _capturer,
                        AudioInputId = await _inputWatcher.GetAndUpdateAsync(),
                        AudioOutputId = await _outputWatcher.GetAndUpdateAsync()
                    };

                    _manager = new VoipManager(descriptor);
                    _manager.StateUpdated += OnStateUpdated;
                    _manager.SignalingDataEmitted += OnSignalingDataEmitted;

                    _manager.Start();

                    await ShowAsync(update.Call, _manager, _capturer, _callStarted);
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

                    if (_manager != null)
                    {
                        _manager.StateUpdated -= OnStateUpdated;
                        _manager.SignalingDataEmitted -= OnSignalingDataEmitted;

                        _manager.SetIncomingVideoOutput(null);
                        _manager.Dispose();
                        _manager = null;
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

                    _call = null;
                }

                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                        if (update.Call.IsOutgoing && discarded.Reason is CallDiscardReasonDeclined)
                        {
                            SoundEffects.Play(SoundEffect.VoipBusy);
                            await ShowAsync(update.Call, null, null, _callStarted);
                        }
                        else
                        {
                            SoundEffects.Stop();
                            await HideAsync();
                        }
                        break;
                    case CallStateError error:
                        await HideAsync();
                        break;
                    default:
                        await ShowAsync(update.Call, _manager, _capturer, _callStarted);
                        break;
                }
            }

            Aggregator.Publish(new UpdateCallDialog(_call));
        }

        private void OnStateUpdated(VoipManager sender, VoipState args)
        {
            if (args is VoipState.WaitInit or VoipState.WaitInitAck)
            {
                SoundEffects.Play(SoundEffect.VoipConnecting);
            }
            else if (args == VoipState.Established)
            {
                _callStarted = DateTime.Now;
                SoundEffects.Stop();
            }
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
                if (_manager != null)
                {
                    _manager.StateUpdated -= OnStateUpdated;
                    _manager.SignalingDataEmitted -= OnSignalingDataEmitted;

                    _manager.SetIncomingVideoOutput(null);
                    _manager.SetVideoCapture(null);

                    _manager.Dispose();
                    _manager = null;
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
            if (_call != null)
            {
                await ShowAsync(_call, _manager, _capturer, _callStarted);
            }
            else
            {
                Aggregator.Publish(new UpdateCallDialog());
            }
        }

        private async Task ShowAsync(Call call, VoipManager controller, IVoipVideoCapture capturer, DateTime started)
        {
            if (_callPage == null)
            {
                var parameters = new ViewServiceParams
                {
                    Width = 720,
                    Height = 540,
                    PersistentId = "Call",
                    Content = control => _callPage = new VoIPPage(ProtoService, CacheService, Aggregator, this)
                };

                _callLifetime = await _viewService.OpenAsync(parameters);
                _callLifetime.Closed += ApplicationView_Released;
                _callLifetime.Released += ApplicationView_Released;
            }

            var callPage = _callPage;
            if (callPage == null)
            {
                return;
            }

            await callPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (controller != null)
                {
                    callPage.Connect(controller);
                }

                callPage.Connect(capturer);
                callPage.Update(call, started);
            });
        }

        private async Task HideAsync()
        {
            _callPage = null;

            var lifetime = _callLifetime;
            if (lifetime != null)
            {
                _callLifetime = null;

                await lifetime.Dispatcher.DispatchAsync(() =>
                {
                    if (lifetime.Window.Content is VoIPPage callPage)
                    {
                        callPage.Dispose();
                    }

                    lifetime.Window.Close();
                });
            }
        }

        private void ApplicationView_Released(object sender, EventArgs e)
        {
            _callPage = null;
            _callLifetime = null;
        }
    }
}
