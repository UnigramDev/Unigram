using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services.ViewService;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Calls;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Calls;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.UI.Xaml;

namespace Telegram.Services.Calls
{
    public partial class VoipCall : VoipCallBase
    {
        public bool IsVideo { get; private set; }

        /// <summary>
        /// True, if the call is outgoing.
        /// </summary>
        public bool IsOutgoing { get; }

        /// <summary>
        /// User identifier of the other call participant.
        /// </summary>
        public long UserId { get; }

        /// <summary>
        /// Call identifier, not persistent.
        /// </summary>
        public int Id { get; }

        public CallState State { get; private set; }

        public int Duration
        {
            get
            {
                if (_readyTime is DateTime startedAt)
                {
                    return (int)(DateTime.Now - startedAt).TotalSeconds;
                }

                return 0;
            }
        }

        public bool IsScreenSharing
        {
            get
            {
                lock (_managerLock)
                {
                    return _camera is VoipScreenCapture && _videoState != VoipVideoState.Inactive;
                }
            }
        }

        public MediaDeviceTracker Devices => _devices;

        private string _videoInputId = "";
        public override string VideoInputId
        {
            get
            {
                lock (_managerLock)
                {
                    return _videoInputId;
                }
            }
            set
            {
                lock (_managerLock)
                {
                    _devices.Track(MediaDeviceClass.VideoInput, value);

                    if (_camera is VoipVideoCapture camera)
                    {
                        camera.SwitchToDevice(_videoInputId = value);
                    }
                }
            }
        }

        private string _audioInputId = "";
        public override string AudioInputId
        {
            get
            {
                lock (_managerLock)
                {
                    return _audioInputId;
                }
            }
            set
            {
                lock (_managerLock)
                {
                    _devices.Track(MediaDeviceClass.AudioInput, value);
                    _manager?.SetAudioInputDevice(_audioInputId = value);
                }
            }
        }

        private string _audioOutputId = "";
        public override string AudioOutputId
        {
            get
            {
                lock (_managerLock)
                {
                    return _audioOutputId;
                }
            }
            set
            {
                lock (_managerLock)
                {
                    _devices.Track(MediaDeviceClass.AudioOutput, value);
                    _manager?.SetAudioOutputDevice(_audioOutputId = value);
                }
            }
        }

        private VoipState _state = VoipState.None;
        private VoipReadyState _readyState = VoipReadyState.Reconnecting;
        private VoipConnectionState _connectionState = VoipConnectionState.Pending;

        private DateTime? _readyTime;

        private readonly object _stateLock = new();

        private readonly MediaDeviceTracker _devices = new();

        private VoipVideoState _videoState = VoipVideoState.Inactive;
        private VoipAudioState _audioState = VoipAudioState.Active;

        public event TypedEventHandler<VoipCall, VoipCallStateChangedEventArgs> StateChanged;
        public event TypedEventHandler<VoipCall, VoipCallConnectionStateChangedEventArgs> ConnectionStateChanged;
        public event TypedEventHandler<VoipCall, VoipCallMediaStateChangedEventArgs> RemoteMediaStateChanged;
        public event TypedEventHandler<VoipCall, VoipCallRemoteBatteryLevelIsLowChangedEventArgs> RemoteBatteryLevelIsLowChanged;
        public event TypedEventHandler<VoipCall, VoipCallAudioLevelUpdatedEventArgs> AudioLevelUpdated;
        public event TypedEventHandler<VoipCall, VoipCallSignalBarsUpdatedEventArgs> SignalBarsUpdated;

        public event TypedEventHandler<VoipCall, VoipCallMediaStateChangedEventArgs> MediaStateChanged;

        public event TypedEventHandler<VoipCall, EventArgs> VideoFailed;

        // TODO: this isn't safe
        private readonly VoipCallAudioLevelUpdatedEventArgs _audioLevelUpdatedArgs = new(0);
        private readonly VoipCallSignalBarsUpdatedEventArgs _signalBarsUpdatedArgs = new(0);

        private volatile VoipAudioState _remoteAudioState = VoipAudioState.Muted;
        private volatile VoipVideoState _remoteVideoState = VoipVideoState.Inactive;

        public VoipCall(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, Call call, VoipState state)
            : base(clientService, settingsService, aggregator)
        {
            IsVideo = call.IsVideo;
            IsOutgoing = call.IsOutgoing;
            UserId = call.UserId;
            Id = call.Id;

            Update(call, state);
        }

        private readonly Queue<IList<byte>> _signalingData = new();

        private readonly object _managerLock = new();
        private VoipManager _manager;
        private VoipCaptureBase _camera;

        public void NeedUpdates()
        {
            lock (_stateLock)
            {
                StateChanged?.Invoke(this, new VoipCallStateChangedEventArgs(_state, _readyState));
                ConnectionStateChanged?.Invoke(this, new VoipCallConnectionStateChangedEventArgs(_connectionState));
            }

            //RemoteMediaStateChanged?.Invoke(this, _remoteMediaStateUpdatedArgs);
            //AudioLevelUpdated?.Invoke(this, _audioLevelUpdatedArgs);
            //SignalBarsUpdated?.Invoke(this, _signalBarsUpdatedArgs);

            lock (_managerLock)
            {
                MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(_audioState, _videoState, IsScreenSharing));
            }
        }

        public VoipAudioState RemoteAudioState => _remoteAudioState;
        public VoipVideoState RemoteVideoState => _remoteVideoState;

        public VoipAudioState AudioState
        {
            get => _audioState;
            set => SetAudioState(value);
        }

        private void SetAudioState(VoipAudioState value)
        {
            lock (_managerLock)
            {
                if (_audioState == value)
                {
                    return;
                }

                _audioState = value;

                if (_manager != null && _manager.IsMuted != (value == VoipAudioState.Muted))
                {
                    _manager.IsMuted = value == VoipAudioState.Muted;
                    MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(value, _videoState, IsScreenSharing));

                    _coordinator?.TryNotifyMutedChanged(value == VoipAudioState.Muted);
                }
                else
                {
                    _coordinator?.TryNotifyMutedChanged(true);
                }
            }
        }

        public VoipVideoState VideoState
        {
            get => _videoState;
            set => SetVideoState(value);
        }

        public void SetVideoState(VoipVideoState value)
        {
            lock (_managerLock)
            {
                if (_videoState == value && _camera is VoipVideoCapture)
                {
                    return;
                }

                _videoState = value;

                if (value != VoipVideoState.Inactive && _camera is not VoipVideoCapture)
                {
                    DisposeCamera();

                    // TODO: device
                    _camera = new VoipVideoCapture(_videoInputId);
                    _camera.FatalErrorOccurred += OnFatalErrorOccurred;
                }

                _camera?.SetState(value);

                if (value == VoipVideoState.Inactive)
                {
                    _manager?.SetVideoCapture(null);
                }
                else
                {
                    _manager?.SetVideoCapture(_camera);
                }

                MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(_audioState, value, IsScreenSharing));
            }
        }

        public void SetLocalVideoOutput(VoipVideoOutput output)
        {
            lock (_managerLock)
            {
                _camera?.SetOutput(output?.Sink);
            }
        }

        public void SetRemoteVideoOutput(VoipVideoOutput output)
        {
            lock (_managerLock)
            {
                _manager?.SetIncomingVideoOutput(output?.Sink);
            }
        }

        public void ShareScreen(GraphicsCaptureItem item)
        {
            lock (_managerLock)
            {
                var state = IsScreenSharing
                    ? VoipVideoState.Active
                    : VoipVideoState.Inactive;

                DisposeCamera();

                var screen = new VoipScreenCapture(item);
                screen.FatalErrorOccurred += OnFatalErrorOccurred;
                screen.Paused += OnPaused;

                _camera = screen;
                _manager.SetVideoCapture(screen);

                if (state != VoipVideoState.Active)
                {
                    _videoState = VoipVideoState.Active;
                    MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(_audioState, VoipVideoState.Active, IsScreenSharing));
                }
            }
        }

        public async void Accept(XamlRoot xamlRoot)
        {
            if (xamlRoot == null)
            {
                ClientService.Send(new AcceptCall(Id, VoipManager.Protocol));

                // TODO: consider delivering a fake update to speed up initialization
            }
            else
            {
                var permissions = await MediaDevicePermissions.CheckAccessAsync(xamlRoot, IsVideo ? MediaDeviceAccess.AudioAndVideo : MediaDeviceAccess.Audio, ElementTheme.Light);
                if (permissions == false)
                {
                    ClientService.Send(new DiscardCall(Id, false, 0, false, 0));
                }
                else
                {
                    ClientService.Send(new AcceptCall(Id, VoipManager.Protocol));
                }
            }
        }

        public override void Discard()
        {
            // TODO: dismiss reason
            ClientService.Send(new DiscardCall(Id, false, Duration, IsVideo, 0));
        }

        public void ReceiveSignalingData(IList<byte> data)
        {
            lock (_managerLock)
            {
                if (_manager != null)
                {
                    _manager.ReceiveSignalingData(data);
                }
                else
                {
                    _signalingData.Enqueue(data);
                }
            }
        }

        public void Update(Call call, VoipState state)
        {
            if (_state >= state)
            {
                return;
            }

            if (_state == VoipState.Ringing)
            {
                SoundEffects.Stop();
            }

            lock (_stateLock)
            {
                _state = state;
                State = call.State;
                StateChanged?.Invoke(this, new VoipCallStateChangedEventArgs(state, VoipReadyState.Reconnecting));

                UpdateConnectionState(VoipReadyState.Reconnecting, 0);
            }

            if (state == VoipState.Requesting || (state == VoipState.Ringing && !call.IsOutgoing))
            {
                TransitionToRequestingOrRinging();
            }
            else if (state == VoipState.Ringing)
            {
                // This gets in only when the call is outgoing
                TransitionToRinging();
            }
            else if (state == VoipState.Connecting)
            {
                TransitionToConnecting();
            }
            // ...
            else if (state == VoipState.Ready)
            {
                TransitionToReady(call.State as CallStateReady);
            }
            else if (state == VoipState.Discarded)
            {
                TransitionToDiscarded(call.State as CallStateDiscarded);
            }
            else if (state == VoipState.Error)
            {
                TransitionToError(call.State as CallStateError);
            }
        }

        private void OnStateUpdated(VoipManager sender, VoipReadyState state)
        {
            if (state is VoipReadyState.WaitInit or VoipReadyState.WaitInitAck)
            {
                SoundEffects.Play(SoundEffect.VoipConnecting);
            }
            else if (state == VoipReadyState.Established)
            {
                SoundEffects.Stop();
            }

            Logger.Info(state);

            lock (_stateLock)
            {
                if (_state != VoipState.Ready || _readyState != state)
                {
                    UpdateConnectionState(state, _signalBarsUpdatedArgs.Count);

                    if (state == VoipReadyState.Established)
                    {
                        _readyTime ??= DateTime.Now;
                    }

                    _readyState = state;
                    _state = VoipState.Ready;
                    StateChanged?.Invoke(this, new VoipCallStateChangedEventArgs(VoipState.Ready, state));
                }
            }
        }

        private void OnSignalBarsUpdated(VoipManager sender, int count)
        {
            lock (_stateLock)
            {
                if (_readyState == VoipReadyState.Reconnecting && _readyTime != null)
                {
                    count = 0;
                }

                if (_signalBarsUpdatedArgs.Count != count)
                {
                    Logger.Info(count);

                    _signalBarsUpdatedArgs.Count = count;
                    SignalBarsUpdated?.Invoke(this, _signalBarsUpdatedArgs);

                    UpdateConnectionState(_readyState, count);
                }
            }
        }

        private void OnAudioLevelUpdated(VoipManager sender, float audioLevel)
        {
            _audioLevelUpdatedArgs.AudioLevel = audioLevel;
            AudioLevelUpdated?.Invoke(this, _audioLevelUpdatedArgs);
        }

        private void UpdateConnectionState(VoipReadyState readyState, int signalBars)
        {
            VoipConnectionState state = VoipConnectionState.Pending;
            if (_state == VoipState.Ready)
            {
                if (readyState == VoipReadyState.Reconnecting && _readyTime != null)
                {
                    state = VoipConnectionState.Error;
                }
                else if (signalBars <= 1 && _readyTime != null)
                {
                    state = VoipConnectionState.Error;
                }
                else if (readyState == VoipReadyState.Established)
                {
                    state = VoipConnectionState.Ready;
                }
            }

            if (_connectionState != state)
            {
                _connectionState = state;
                ConnectionStateChanged?.Invoke(this, new VoipCallConnectionStateChangedEventArgs(state));
            }
        }

        private void OnRemoteMediaStateUpdated(VoipManager sender, RemoteMediaStateUpdatedEventArgs args)
        {
            if (_remoteAudioState != args.Audio || _remoteVideoState != args.Video)
            {
                _remoteAudioState = args.Audio;
                _remoteVideoState = args.Video;
                RemoteMediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(args.Audio, args.Video, false));
            }
        }

        private void OnRemoteBatteryLevelIsLowUpdated(VoipManager sender, bool remoteBatteryLevelIsLow)
        {
            RemoteBatteryLevelIsLowChanged?.Invoke(this, new VoipCallRemoteBatteryLevelIsLowChangedEventArgs(remoteBatteryLevelIsLow));
        }

        private void OnSignalingDataEmitted(VoipManager sender, SignalingDataEmittedEventArgs args)
        {
            ClientService.Send(new SendCallSignalingData(Id, args.Data));
        }

        private void OnDeviceChanged(object sender, MediaDeviceChangedEventArgs e)
        {
            switch (e.DeviceClass)
            {
                case MediaDeviceClass.VideoInput:
                    lock (_managerLock)
                    {
                        if (_camera is VoipVideoCapture camera)
                        {
                            camera.SwitchToDevice(_videoInputId = e.DeviceId);
                        }
                    }
                    break;
                case MediaDeviceClass.AudioInput:
                    lock (_managerLock)
                    {
                        _manager?.SetAudioInputDevice(_audioInputId = e.DeviceId);
                    }
                    break;
                case MediaDeviceClass.AudioOutput:
                    lock (_managerLock)
                    {
                        _manager?.SetAudioOutputDevice(_audioOutputId = e.DeviceId);
                    }
                    break;
            }
        }

        private void TransitionToRequestingOrRinging()
        {
            if (ClientService.TryGetUser(UserId, out User user))
            {
                Logger.Info("Waiting for call creation");
                InitializeSystemCallAsync(user, IsOutgoing).Wait();
            }

            // If the system request fails, we still want to ring and show the call window
            if (_systemCall == null || IsOutgoing)
            {
                SoundEffects.Play(IsOutgoing ? SoundEffect.VoipRingback : SoundEffect.VoipIncoming);
                CreateWindow(IsOutgoing);
            }
        }

        private void TransitionToRinging()
        {
            SoundEffects.Play(SoundEffect.VoipRingback);
        }

        private void TransitionToConnecting()
        {
            SoundEffects.Play(SoundEffect.VoipConnecting);

            if (_systemCall == null || IsOutgoing)
            {
                return;
            }

            CreateWindow(true);
        }

        private void TransitionToReady(CallStateReady ready)
        {
            //var logFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", $"voip{Id}.txt");
            //var statsDumpFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{SessionId}", "tgvoip.statsDump.txt");

            var call_packet_timeout_ms = ClientService.Options.CallPacketTimeoutMs;
            var call_connect_timeout_ms = ClientService.Options.CallConnectTimeoutMs;

            var descriptor = new VoipDescriptor
            {
                Version = ready.Protocol.LibraryVersions[0],
                CustomParameters = ready.CustomParameters,
                InitializationTimeout = call_packet_timeout_ms / 1000.0,
                ReceiveTimeout = call_connect_timeout_ms / 1000.0,
                Servers = ready.Servers,
                EncryptionKey = ready.EncryptionKey,
                IsOutgoing = IsOutgoing,
                EnableP2p = ready.Protocol.UdpP2p && ready.AllowP2p,
                VideoCapture = _camera,
                AudioInputId = _audioInputId,
                AudioOutputId = _audioOutputId
            };

            var manager = new VoipManager();
            manager.StateUpdated += OnStateUpdated;
            manager.SignalBarsUpdated += OnSignalBarsUpdated;
            manager.AudioLevelUpdated += OnAudioLevelUpdated;
            manager.RemoteMediaStateUpdated += OnRemoteMediaStateUpdated;
            manager.RemoteBatteryLevelIsLowUpdated += OnRemoteBatteryLevelIsLowUpdated;
            manager.SignalingDataEmitted += OnSignalingDataEmitted;
            manager.Start(descriptor);

            lock (_managerLock)
            {
                _manager = manager;
                _audioState = manager.IsMuted ? VoipAudioState.Muted : VoipAudioState.Active;

                while (_signalingData.TryDequeue(out var data))
                {
                    _manager.ReceiveSignalingData(data);
                }
            }

            _devices.Changed += OnDeviceChanged;

            _coordinator?.TryNotifyMutedChanged(manager.IsMuted);
        }

        private void TransitionToDiscarded(CallStateDiscarded discarded)
        {
            DisposeImpl();

            if (IsOutgoing && discarded?.Reason is CallDiscardReasonDeclined)
            {
                SoundEffects.Play(SoundEffect.VoipBusy);
            }
            else if (_readyTime != null)
            {
                // TODO: check if call was in progress
                SoundEffects.Play(SoundEffect.VoipEnd);
            }
        }

        private void TransitionToError(CallStateError error)
        {
            DisposeImpl();

            // TODO: check if call was in progress
            SoundEffects.Play(SoundEffect.VoipFailed);
        }

        private void CreateWindow(bool newOutgoingCall)
        {
            var service = TypeResolver.Current.Resolve<IViewService>(int.MaxValue);
            var options = new ViewServiceOptions
            {
                Width = newOutgoingCall ? 720 : 320,
                Height = newOutgoingCall ? 540 : 320,
                PersistedId = "Call",
                Content = CreatePresentation,
                ViewMode = newOutgoingCall
                    ? ViewServiceMode.Default
                    : ViewServiceMode.CompactOverlay,
            };

            Logger.Info("Waiting for window creation");
            _ = service.OpenAsync(options);
        }

        private UIElement CreatePresentation(ViewLifetimeControl control)
        {
            // Initialize video now, so that permissions are asked on the right window
            // TODO: this won't work for now, because WebRTC always initializes MediaCapture on the main thread

            lock (_managerLock)
            {
                if (IsVideo && _camera == null)
                {
                    _camera = new VoipVideoCapture(_videoInputId);
                    _camera.FatalErrorOccurred += OnFatalErrorOccurred;

                    _videoState = VoipVideoState.Active;
                }
            }

            return new VoipPage(this);
        }

        public override void Show()
        {
            WindowContext.Activate("Call");
        }

        private void DisposeImpl()
        {
            if (_systemCall != null)
            {
                _systemCall.EndRequested -= OnEndRequested;
                _systemCall.AnswerRequested -= OnAnswerRequested;
                _systemCall.RejectRequested -= OnRejectRequested;
                _systemCall.TryNotifyCallEnded();
                _systemCall = null;
            }

            if (_coordinator != null)
            {
                _coordinator.MuteStateChanged -= OnMuteStateChanged;
                _coordinator = null;
            }

            _devices.Changed -= OnDeviceChanged;
            _devices.Stop();

            lock (_managerLock)
            {
                DisposeCamera();

                if (_manager != null)
                {
                    _manager.StateUpdated -= OnStateUpdated;
                    _manager.SignalBarsUpdated -= OnSignalBarsUpdated;
                    _manager.AudioLevelUpdated -= OnAudioLevelUpdated;
                    _manager.RemoteMediaStateUpdated -= OnRemoteMediaStateUpdated;
                    _manager.RemoteBatteryLevelIsLowUpdated -= OnRemoteBatteryLevelIsLowUpdated;
                    _manager.SignalingDataEmitted -= OnSignalingDataEmitted;

                    _manager.SetVideoCapture(null);

                    _manager.Stop();
                    _manager = null;
                }
            }
        }

        private void DisposeCamera()
        {
            // This method expects _managerLock to be locked
            if (_camera != null)
            {
                if (_camera is VoipScreenCapture screen)
                {
                    screen.Paused -= OnPaused;
                }

                _camera.FatalErrorOccurred -= OnFatalErrorOccurred;

                _camera.SetOutput(null);

                _camera.Stop();
                _camera = null;
            }
        }

        private void OnPaused(VoipScreenCapture sender, bool paused)
        {
            lock (_managerLock)
            {
                if (_camera == sender && _videoState != VoipVideoState.Inactive)
                {
                    var state = paused
                        ? VoipVideoState.Paused
                        : VoipVideoState.Active;

                    if (_videoState != state)
                    {
                        _videoState = state;
                        MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(_audioState, state, true));
                    }
                }
            }
        }

        private void OnFatalErrorOccurred(VoipCaptureBase sender, object args)
        {
            lock (_managerLock)
            {
                if (_camera == sender)
                {
                    if (_videoState != VoipVideoState.Inactive)
                    {
                        _videoState = VoipVideoState.Inactive;
                        MediaStateChanged?.Invoke(this, new VoipCallMediaStateChangedEventArgs(_audioState, VoipVideoState.Inactive, false));
                    }

                    if (sender is VoipVideoCapture)
                    {
                        VideoFailed?.Invoke(this, EventArgs.Empty);
                    }

                    DisposeCamera();
                }
            }
        }

        #region System call

        private VoipCallCoordinator _coordinator;
        private VoipPhoneCall _systemCall;

        private async Task InitializeSystemCallAsync(User user, bool outgoing)
        {
            try
            {
                // GetDefault may throw already
                var coordinator = ApiInfo.IsVoipSupported ? VoipCallCoordinator.GetDefault() : null;
                var status = await coordinator.TryReserveCallResourcesAsync();

                if (status == VoipPhoneCallResourceReservationStatus.Success)
                {
                    _coordinator = coordinator;
                    _coordinator.MuteStateChanged += OnMuteStateChanged;

                    if (outgoing)
                    {
                        // I'm not sure if RequestNewOutgoingCall is the right method to call, but it seem to work.
                        // TODO: this moves the focus from the call window to the main window :(
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

                        // TODO: this moves the focus from the call window to the main window :(
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

            WatchDog.TrackEvent("VoipCall", new Properties
            {
                { "Requested", _systemCall != null },
                { "Outgoing", outgoing },
                { "DeviceFamily", Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily }
            });
        }

        private void OnMuteStateChanged(VoipCallCoordinator sender, MuteChangeEventArgs args)
        {
            AudioState = args.Muted
                ? VoipAudioState.Muted
                : VoipAudioState.Active;
        }

        private void OnAnswerRequested(VoipPhoneCall sender, CallAnswerEventArgs args)
        {
            try
            {
                sender.TryShowAppUI();

                // NotifyCallActive causes the main app window to be focused
                // We call it immediately, so that the focus can move to the call window.
                sender.NotifyCallActive();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            IsVideo = args.AcceptedMedia.HasFlag(VoipPhoneCallMedia.Video);
            Accept(null);
        }

        private void OnRejectRequested(VoipPhoneCall sender, CallRejectEventArgs args)
        {
            Discard();
        }

        private void OnEndRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            Discard();
        }

        #endregion
    }
}
