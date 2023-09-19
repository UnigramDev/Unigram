//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services.Updates;
using Telegram.Services.ViewService;
using Telegram.Td.Api;
using Telegram.Views.Calls;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public enum VoipCaptureType
    {
        None,
        Video,
        Screencast
    }

    public interface IVoipService
    {
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }
        string CurrentVideoInput { get; set; }

        void SetVideoInput(string value, CanvasControl canvas);

#if ENABLE_CALLS
        bool IsMuted { get; set; }
        event EventHandler MutedChanged;

        VoipManager Manager { get; }
        VoipCaptureBase Capturer { get; set; }

        CallProtocol Protocol { get; }
#endif

        Call Call { get; }
        DateTime? CallStarted { get; }

        void Show();

        void Start(long chatId, bool video);

        Task DiscardAsync();

#if ENABLE_CALLS
        VoipCaptureType CaptureType { get; }
        Task<VoipCaptureBase> ToggleCapturingAsync(VoipCaptureType type);
#endif
    }

    public class VoipService : ViewModelBase
        , IVoipService
    //, IHandle<UpdateCall>
    //, IHandle<UpdateNewCallSignalingData>
    {
        private readonly IViewService _viewService;

        private readonly MediaDeviceWatcher _videoWatcher;

        private readonly MediaDeviceWatcher _inputWatcher;
        private readonly MediaDeviceWatcher _outputWatcher;

        private readonly DisposableMutex _updateLock = new DisposableMutex();

        private Call _call;
        private DateTime? _callStarted;

#if ENABLE_CALLS
        private VoipManager _manager;
        private VoipCaptureBase _capturer;

        private CallPage _callPage;

        private VoipCallCoordinator _coordinator;
        private VoipPhoneCall _systemCall;
#endif

        private ViewLifetimeControl _callLifetime;

        public VoipService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(clientService, settingsService, aggregator)
        {
            _viewService = viewService;

#if ENABLE_CALLS
            _videoWatcher = new MediaDeviceWatcher(DeviceClass.VideoCapture, id => SetVideoInputDevice(id));
            _inputWatcher = new MediaDeviceWatcher(DeviceClass.AudioCapture, id => _manager?.SetAudioInputDevice(id));
            _outputWatcher = new MediaDeviceWatcher(DeviceClass.AudioRender, id => _manager?.SetAudioOutputDevice(id));
#endif

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateCall>(this, Handle)
                .Subscribe<UpdateNewCallSignalingData>(Handle);
        }

#if ENABLE_CALLS

        public bool IsMuted
        {
            get => _manager.IsMuted;
            set
            {
                if (_manager != null && _manager.IsMuted != value)
                {
                    _manager.IsMuted = value;
                    MutedChanged?.Invoke(_manager, EventArgs.Empty);

                    _coordinator?.TryNotifyMutedChanged(value);
                }
                else
                {
                    _coordinator?.TryNotifyMutedChanged(true);
                }
            }
        }

        public event EventHandler MutedChanged;

        private void SetVideoInputDevice(string id)
        {
            if (_capturer is VoipVideoCapture capturer)
            {
                capturer.SwitchToDevice(id);
            }
        }

        private bool _wasVideoEnabled;

        public async Task<VoipCaptureBase> ToggleCapturingAsync(VoipCaptureType type)
        {
            void Disable()
            {
                if (_capturer != null)
                {
                    _capturer.SetOutput(null);
                    _manager?.SetVideoCapture(null);

                    _capturer.Dispose();
                    _capturer = null;
                }
            }

            if (type == VoipCaptureType.None)
            {
                if (_wasVideoEnabled && CaptureType == VoipCaptureType.Screencast)
                {
                    return await ToggleCapturingAsync(VoipCaptureType.Video);
                }
                else
                {
                    _wasVideoEnabled = false;
                    Disable();
                }
            }
            else if (type == VoipCaptureType.Video && _capturer is not VoipVideoCapture)
            {
                Disable();

                if (_manager != null)
                {
                    _capturer = new VoipVideoCapture(await _videoWatcher.GetAndUpdateAsync());
                    _manager?.SetVideoCapture(_capturer);

                    _wasVideoEnabled = true;
                }
            }
            else if (type == VoipCaptureType.Screencast && _capturer is not VoipScreenCapture)
            {
                if (_manager == null || !VoipScreenCapture.IsSupported())
                {
                    return null;
                }

                var picker = new GraphicsCapturePicker();
                var item = await picker.PickSingleItemAsync();

                if (item == null)
                {
                    return null;
                }

                Disable();

                if (_manager != null)
                {
                    var screenCapturer = new VoipScreenCapture(item);
                    screenCapturer.FatalErrorOccurred += OnFatalErrorOccurred;

                    _capturer = screenCapturer;
                    _manager?.SetVideoCapture(_capturer);
                }
            }

            return _capturer;
        }

        private void OnFatalErrorOccurred(VoipScreenCapture sender, object args)
        {
            if (_capturer != null)
            {
                _capturer.SetOutput(null);
                _manager.SetVideoCapture(null);

                _capturer.Dispose();
                _capturer = null;
            }
        }

        public VoipManager Manager => _manager;
        public VoipCaptureBase Capturer
        {
            get => _capturer;
            set => _capturer = value;
        }

        public CallProtocol Protocol => VoipManager.Protocol;

        public VoipCaptureType CaptureType => _capturer is VoipVideoCapture
            ? VoipCaptureType.Video
            : _capturer is VoipScreenCapture
            ? VoipCaptureType.Screencast
            : VoipCaptureType.None;

#endif

        public async void Start(long chatId, bool video)
        {
            var chat = ClientService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var call = Call;
            if (call != null)
            {
                var callUser = ClientService.GetUser(call.UserId);
                if (callUser != null && callUser.Id != user.Id)
                {
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.VoipOngoingAlert, callUser.FullName(), user.FullName()), Strings.VoipOngoingAlertTitle, Strings.OK, Strings.Cancel);
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

            var fullInfo = ClientService.GetUserFull(user.Id);
            if (fullInfo != null && fullInfo.HasPrivateCalls)
            {
                await MessagePopup.ShowAsync(string.Format(Strings.CallNotAvailable, user.FirstName), Strings.VoipFailed, Strings.OK);
                return;
            }

            var permissions = await MediaDeviceWatcher.CheckAccessAsync(video, false);
            if (permissions == false)
            {
                return;
            }

            var protocol = VoipManager.Protocol;

            var response = await ClientService.SendAsync(new CreateCall(user.Id, protocol, video));
            if (response is Error error)
            {
                if (error.Code == 400 && error.Message.Equals("PARTICIPANT_VERSION_OUTDATED"))
                {
                    var message = video
                        ? Strings.VoipPeerVideoOutdated
                        : Strings.VoipPeerOutdated;
                    await MessagePopup.ShowAsync(string.Format(message, user.FirstName), Strings.AppName, Strings.OK);
                }
                else if (error.Code == 400 && error.Message.Equals("USER_PRIVACY_RESTRICTED"))
                {
                    await MessagePopup.ShowAsync(string.Format(Strings.CallNotAvailable, user.FullName()), Strings.AppName, Strings.OK);
                }
            }
        }

        public Task DiscardAsync()
        {
            if (Call != null)
            {
                return ClientService.SendAsync(new DiscardCall(Call.Id, false, 0, false, 0));
            }

            return Task.CompletedTask;
        }

        private async Task InitializeSystemCallAsync(User user, bool outgoing)
        {
            if (_systemCall != null || !ApiInfo.IsVoipSupported)
            {
                return;
            }

            var coordinator = VoipCallCoordinator.GetDefault();
            var status = VoipPhoneCallResourceReservationStatus.ResourcesNotAvailable;

            try
            {
                status = await coordinator.ReserveCallResourcesAsync();
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024713)
                {
                    // CPU and memory resources have already been reserved for the app.
                    // Ignore the return value from your call to ReserveCallResourcesAsync,
                    // and proceed to handle a new VoIP call.
                    status = VoipPhoneCallResourceReservationStatus.Success;
                }
            }

            try
            {
                if (status == VoipPhoneCallResourceReservationStatus.Success)
                {
                    _coordinator = coordinator;
                    _coordinator.MuteStateChanged += OnMuteStateChanged;

                    if (outgoing)
                    {
                        // I'm not sure if RequestNewOutgoingCall is the right method to call, but it seem to work.
                        _systemCall = _coordinator.RequestNewOutgoingCall(
                            $"{user.Id}",
                            user.FullName(),
                            Strings.AppName,
                            VoipPhoneCallMedia.Audio | VoipPhoneCallMedia.Video);
                        //_systemCall.TryNotifyCallActive();
                    }
                    else
                    {
                        static Uri GetPhoto(User user)
                        {
                            if (user.ProfilePhoto != null && user.ProfilePhoto.Small.Local.IsDownloadingCompleted)
                            {
                                return new Uri(user.ProfilePhoto.Small.Local.Path);
                            }

                            return null;
                        }

                        _systemCall = _coordinator.RequestNewIncomingCall(
                            $"{user.Id}",
                            user.FullName(),
                            user.PhoneNumber,
                            GetPhoto(user),
                            Strings.AppName,
                            new Uri(Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Logos\\LockScreenLogo.png")),
                            string.Empty,
                            null,
                            VoipPhoneCallMedia.Audio | VoipPhoneCallMedia.Video,
                            TimeSpan.FromSeconds(120));
                        _systemCall.AnswerRequested += OnAnswerRequested;
                        _systemCall.RejectRequested += OnRejectRequested;
                    }

                    _systemCall.EndRequested += OnEndRequested;
                }
            }
            catch
            {
                _coordinator = null;
                _systemCall = null;
            }
        }

        private void OnMuteStateChanged(VoipCallCoordinator sender, MuteChangeEventArgs args)
        {
            IsMuted = args.Muted;
        }

        private void OnAnswerRequested(VoipPhoneCall sender, CallAnswerEventArgs args)
        {
            if (Call is Call call)
            {
                ClientService.Send(new AcceptCall(call.Id, Protocol));
            }

            try
            {
                sender.TryShowAppUI();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void OnRejectRequested(VoipPhoneCall sender, CallRejectEventArgs args)
        {
            if (Call is Call call)
            {
                ClientService.Send(new DiscardCall(call.Id, false, 0, false, 0));
            }
        }

        private void OnEndRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            Aggregator.Publish(new UpdateCall(new Call { State = new CallStateDiscarded { Reason = new CallDiscardReasonEmpty() } }));
        }

        public string CurrentVideoInput
        {
            get => _videoWatcher.Get();
            set => _videoWatcher.Set(value);
        }

        public void SetVideoInput(string value, CanvasControl canvas)
        {
            _videoWatcher.Set(value);

#if ENABLE_CALLS
            if (_capturer is VoipVideoCapture capturer)
            {
                capturer.SetOutput(canvas, false);
            }
#endif
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
#if ENABLE_CALLS
            _manager?.ReceiveSignalingData(update.Data);
#endif
        }

        public async void Handle(UpdateCall update)
        {
#if ENABLE_CALLS
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
                        if (_systemCall == null || update.Call.IsOutgoing)
                        {
                            SoundEffects.Play(update.Call.IsOutgoing ? SoundEffect.VoipRingback : SoundEffect.VoipIncoming);
                        }

                        var user = ClientService.GetUser(update.Call.UserId);
                        if (user == null)
                        {
                            return;
                        }

                        await InitializeSystemCallAsync(user, update.Call.IsOutgoing);
                    }
                }
                else if (update.Call.State is CallStateExchangingKeys exchangingKeys)
                {
                    SoundEffects.Play(SoundEffect.VoipConnecting);
                }
                else if (update.Call.State is CallStateReady ready)
                {
                    var user = ClientService.GetUser(update.Call.UserId);
                    if (user == null)
                    {
                        return;
                    }

                    //VoIPControllerWrapper.UpdateServerConfig(ready.Config);

                    _systemCall?.TryNotifyCallActive();
                    //await InitializeSystemCallAsync(user, false);

                    var logFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", $"voip{update.Call.Id}.txt");
                    var statsDumpFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.statsDump.txt");

                    var call_packet_timeout_ms = ClientService.Options.CallPacketTimeoutMs;
                    var call_connect_timeout_ms = ClientService.Options.CallConnectTimeoutMs;

                    if (_manager != null)
                    {
                        //_controller.Dispose();
                        _manager = null;
                    }

                    _capturer = update.Call.IsVideo
                        ? new VoipVideoCapture(await _videoWatcher.GetAndUpdateAsync())
                        : null;

                    var version = ready.Protocol.LibraryVersions[0];
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

                    _manager = new VoipManager(version, descriptor);
                    _manager.StateUpdated += OnStateUpdated;
                    _manager.SignalingDataEmitted += OnSignalingDataEmitted;

                    _manager.Start();
                    _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);

                    await ShowAsync(update.Call, _manager, _capturer);
                }
                else if (update.Call.State is CallStateDiscarded discarded)
                {
                    if (discarded.NeedDebugInformation)
                    {
                        //ClientService.Send(new SendCallDebugInformation(update.Call.Id, _controller.GetDebugLog()));
                    }

                    if (discarded.NeedRating)
                    {
                        BeginOnUIThread(async () => await SendRatingAsync(update.Call.Id));
                    }

                    Dispose();

                    _call = null;
                }
                else if (update.Call.State is CallStateError error)
                {
                    if (string.Equals(error.Error.Message, "PARTICIPANT_VERSION_OUTDATED", StringComparison.OrdinalIgnoreCase))
                    {
                        var user = ClientService.GetUser(update.Call.UserId);
                        if (user == null)
                        {
                            return;
                        }

                        var message = update.Call.IsVideo
                            ? Strings.VoipPeerVideoOutdated
                            : Strings.VoipPeerOutdated;
                        BeginOnUIThread(async () => await MessagePopup.ShowAsync(string.Format(message, user.FirstName), Strings.AppName, Strings.OK));
                    }

                    _call = null;
                }

                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                        if (update.Call.IsOutgoing && discarded.Reason is CallDiscardReasonDeclined)
                        {
                            SoundEffects.Play(SoundEffect.VoipBusy);
                            await ShowAsync(update.Call, null, null);
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
                        await ShowAsync(update.Call, _manager, _capturer);
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
                _callStarted ??= DateTime.Now;
                SoundEffects.Stop();
            }
        }

        private void OnSignalingDataEmitted(VoipManager sender, SignalingDataEmittedEventArgs args)
        {
            var call = _call;
            if (call != null)
            {
                ClientService.Send(new SendCallSignalingData(_call.Id, args.Data));
            }
            else
            {
                Dispose();
            }
#endif
        }

        private void Dispose()
        {
            if (_capturer != null)
            {
                _capturer.SetOutput(null);
                _capturer.Dispose();
                _capturer = null;
            }

            if (_manager != null)
            {
                _manager.StateUpdated -= OnStateUpdated;
                _manager.SignalingDataEmitted -= OnSignalingDataEmitted;

                _manager.SetIncomingVideoOutput(null);
                _manager.SetVideoCapture(null);

                _manager.Dispose();
                _manager = null;
            }

            try
            {
                if (_coordinator != null)
                {
                    _coordinator.MuteStateChanged -= OnMuteStateChanged;
                    _coordinator = null;
                }

                if (_systemCall != null)
                {
                    _systemCall.TryNotifyCallEnded();
                    _systemCall.AnswerRequested -= OnAnswerRequested;
                    _systemCall.RejectRequested -= OnRejectRequested;
                    _systemCall.EndRequested -= OnEndRequested;
                    _systemCall = null;
                }
            }
            catch
            {
                _coordinator = null;
                _systemCall = null;
            }
        }

        private async Task SendRatingAsync(int callId)
        {
            var dialog = new CallRatingPopup();

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                // We need updates here
                //await ClientService.SendAsync(new SendCallRating(callId, dialog.Rating, dialog.Rating >= 1 && dialog.Rating <= 4 ? dialog.Comment : string.Empty));

                if (dialog.IncludeDebugLogs && dialog.Rating <= 3)
                {
                    var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(Path.Combine($"{SessionId}", $"voip{callId}.txt")) as StorageFile;
                    if (file == null)
                    {
                        return;
                    }

                    var chat = await ClientService.SendAsync(new CreatePrivateChat(4244000, false)) as Chat;
                    if (chat == null)
                    {
                        return;
                    }

                    ClientService.Send(new SendMessage(chat.Id, 0, null, null, null, new InputMessageDocument(new InputFileLocal(file.Path), null, false, null)));
                }
            }
        }

        public Call Call => _call;
        public DateTime? CallStarted => _callStarted;

        public async void Show()
        {
#if ENABLE_CALLS
            if (_call != null)
            {
                await ShowAsync(_call, _manager, _capturer);
            }
            else
            {
                Aggregator.Publish(new UpdateCallDialog());
            }
        }

        private async Task ShowAsync(Call call, VoipManager controller, VoipCaptureBase capturer)
        {
            if (call.State is CallStatePending && !call.IsOutgoing && _systemCall != null)
            {
                return;
            }

            if (_callPage == null)
            {
                var parameters = new ViewServiceParams
                {
                    Width = 720,
                    Height = 540,
                    PersistentId = "Call",
                    Content = control => _callPage = new CallPage(ClientService, Aggregator, this)
                };

                _callLifetime = await _viewService.OpenAsync(parameters);
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
                callPage.Update(call);
            });
        }

        private async Task HideAsync()
        {
            _callPage = null;

            var lifetime = _callLifetime;
            if (lifetime != null)
            {
                _callLifetime = null;

                await lifetime.Dispatcher.DispatchAsync(async () =>
                {
                    if (lifetime.Window.Content is CallPage callPage)
                    {
                        callPage.Dispose();
                    }

                    await WindowContext.Current.ConsolidateAsync();
                });
            }
        }

        private void ApplicationView_Released(object sender, EventArgs e)
        {
            _callPage = null;
            _callLifetime = null;
#endif
        }
    }
}
