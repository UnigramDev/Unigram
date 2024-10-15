//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Calls;
using Telegram.Views.Calls.Popups;
using Windows.ApplicationModel.Calls;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services.Calls
{
    public partial class VoipGroupCall : VoipCallBase
    {
        private readonly IViewService _viewService;

        private Chat _chat;

        private MessageSender _alias;
        private MessageSenders _availableAliases;
        private TaskCompletionSource<MessageSenders> _availableAliasesTask;

        private GroupCallParticipant _currentUser;

        private TimeSpan _timeDifference;

        private readonly object _managerLock = new();

        private VoipGroupManager _manager;
        private VoipVideoCapture _capturer;
        private int _source;

        private VoipGroupManager _screenManager;
        private VoipScreenCapture _screenCapturer;
        //private EventDebouncer<bool> _screenDebouncer;
        private int _screenSource;

        private readonly MediaDeviceTracker _devices = new();

        private VoipCallCoordinator _coordinator;
        private VoipPhoneCall _systemCall;

        private bool _isScheduled;
        private bool _isConnected;
        private bool _isClosed;

        private int _availableStreamsCount = 0;

        public VoipGroupCall(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, XamlRoot xamlRoot, Chat chat, GroupCall groupCall, MessageSender alias)
            : base(clientService, settingsService, aggregator)
        {
            Duration = groupCall.Duration;
            IsVideoRecorded = groupCall.IsVideoRecorded;
            RecordDuration = groupCall.RecordDuration;
            CanToggleMuteNewParticipants = groupCall.CanToggleMuteNewParticipants;
            MuteNewParticipants = groupCall.MuteNewParticipants;
            CanEnableVideo2 = groupCall.CanEnableVideo;
            IsMyVideoPaused = groupCall.IsMyVideoPaused;
            IsMyVideoEnabled = groupCall.IsMyVideoEnabled;
            RecentSpeakers = groupCall.RecentSpeakers;
            LoadedAllParticipants = groupCall.LoadedAllParticipants;
            HasHiddenListeners = groupCall.HasHiddenListeners;
            ParticipantCount = groupCall.ParticipantCount;
            CanBeManaged = groupCall.CanBeManaged;
            NeedRejoin = groupCall.NeedRejoin;
            IsJoined = groupCall.IsJoined;
            IsRtmpStream = groupCall.IsRtmpStream;
            IsActive = groupCall.IsActive;
            EnabledStartNotification = groupCall.EnabledStartNotification;
            ScheduledStartDate = groupCall.ScheduledStartDate;
            Title = groupCall.Title;
            Id = groupCall.Id;

            var unix = ClientService.SendAsync(new GetOption("unix_time")).Result as OptionValueInteger;

            _timeDifference = DateTime.Now - Formatter.ToLocalTime(unix.Value);

            _chat = chat;

            _isScheduled = groupCall.ScheduledStartDate > 0;

            _devices.Changed += OnDeviceChanged;

            var descriptor = new VoipGroupDescriptor
            {
                //AudioInputId = _inputWatcher.GetAndUpdateAsync().Result,
                //AudioOutputId = _outputWatcher.GetAndUpdateAsync().Result,
                IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled
            };

            _manager = new VoipGroupManager(descriptor);
            _manager.NetworkStateUpdated += OnNetworkStateUpdated;
            _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _manager.BroadcastTimeRequested += OnBroadcastTimeRequested;
            _manager.BroadcastPartRequested += OnBroadcastPartRequested;

            _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);

            InitializeSystemCallAsync(chat).Wait();
            CreateWindow();

            if (groupCall.ScheduledStartDate > 0)
            {
                IsJoined = true;
                ClientService.Send(new SetVideoChatDefaultParticipant(chat.Id, alias));
            }
            else
            {
                Rejoin(alias);
            }
        }

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallNetworkStateChangedEventArgs> NetworkStateChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallJoinedStateChangedEventArgs> JoinedStateChanged;

        public event EventHandler AvailableStreamsChanged;
        public int AvailableStreamsCount => _availableStreamsCount;

        private GroupCallParticipantsCollection _participants;
        public GroupCallParticipantsCollection Participants
        {
            get => _participants;
            private set => Set(ref _participants, value);
        }

        public async Task<bool> CanChooseAliasAsync()
        {
            var aliases = _availableAliases;
            if (aliases != null)
            {
                return aliases.TotalCount > 1;
            }

            if (_availableAliasesTask != null)
            {
                var response = await _availableAliasesTask.Task;
                return response?.TotalCount > 1;
            }

            _availableAliasesTask = new TaskCompletionSource<MessageSenders>();

            var result = await CanChooseAliasAsyncInternal(Chat.Id);
            return result?.TotalCount > 1;
        }

        private async Task<MessageSenders> CanChooseAliasAsyncInternal(long chatId)
        {
            var response = await ClientService.SendAsync(new GetVideoChatAvailableParticipants(chatId));
            if (response is MessageSenders senders)
            {
                _availableAliases = senders;
                _availableAliasesTask?.TrySetResult(senders);
                return senders;
            }

            return null;
        }

        private async Task InitializeSystemCallAsync(Chat chat)
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

                    // I'm not sure if RequestNewOutgoingCall is the right method to call, but it seem to work.
                    // TODO: this moves the focus from the call window to the main window :(
                    _systemCall = _coordinator.RequestNewOutgoingCall($"{chat.Id}", chat.Title, Strings.AppName, VoipPhoneCallMedia.Audio | VoipPhoneCallMedia.Video);
                    _systemCall.TryNotifyCallActive();
                    _systemCall.EndRequested += OnEndRequested;
                }
            }
            catch
            {
                _coordinator = null;
                _systemCall = null;
            }

            WatchDog.TrackEvent("VoipGroupCall", new Properties
            {
                { "Requested", _systemCall != null },
                { "DeviceFamily", Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily }
            });
        }

        private void OnMuteStateChanged(VoipCallCoordinator sender, MuteChangeEventArgs args)
        {
            IsMuted = args.Muted;
        }

        private void OnEndRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            Discard();
        }

        public async Task RejoinAsync(XamlRoot xamlRoot)
        {
            var available = await CanChooseAliasAsyncInternal(_chat.Id);
            if (available != null)
            {
                var popup = new VideoChatAliasesPopup(ClientService, _chat, false, _availableAliases.Senders.ToArray());
                popup.RequestedTheme = ElementTheme.Dark;

                var confirm = await popup.ShowQueuedAsync(xamlRoot);
                if (confirm == ContentDialogResult.Primary && (IsJoined || NeedRejoin))
                {
                    Rejoin(popup.SelectedSender);
                }
            }
        }

        private void Rejoin(MessageSender alias)
        {
            _alias = alias;

            _manager?.SetConnectionMode(VoipGroupConnectionMode.None, false, IsRtmpStream);
            _manager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                if (_manager == null)
                {
                    return;
                }

                if (IsRtmpStream)
                {
                    Participants = null;
                }
                else
                {
                    Participants ??= new GroupCallParticipantsCollection(this);
                }

                var response = await ClientService.SendAsync(new JoinGroupCall(Id, alias, ssrc, payload, _manager.IsMuted, _capturer != null, string.Empty));
                if (response is Text json && _manager != null)
                {
                    bool broadcast;
                    if (JsonObject.TryParse(json.TextValue, out JsonObject data))
                    {
                        broadcast = data.GetNamedBoolean("stream", false);
                    }
                    else
                    {
                        broadcast = false;
                    }

                    _source = ssrc;
                    _manager.SetConnectionMode(broadcast ? VoipGroupConnectionMode.Broadcast : VoipGroupConnectionMode.Rtc, true, IsRtmpStream);
                    _manager.SetJoinResponsePayload(json.TextValue);

                    RejoinScreenSharing();
                }
            });
        }

        #region Capturing

        public bool CanEnableVideo
        {
            get
            {
                if (CanEnableVideo2)
                {
                    var currentUser = _currentUser;
                    if (currentUser != null)
                    {
                        return !(currentUser.IsMutedForAllUsers && !currentUser.CanUnmuteSelf);
                    }
                }

                return false;
            }
        }

        public bool IsVideoEnabled => _capturer != null;

        public void ToggleCapturing()
        {
            if (_manager == null)
            {
                return;
            }

            if (_capturer != null)
            {
                _capturer.SetOutput(null);
                _manager.SetVideoCapture(null);

                _capturer.Stop();
                _capturer = null;
            }
            else
            {
                _capturer = new VoipVideoCapture(_videoInputId);
                _manager.SetVideoCapture(_capturer);
            }

            ClientService.Send(new ToggleGroupCallIsMyVideoEnabled(Id, _capturer != null));
        }

        #endregion

        #region Screencast

        public bool IsScreenSharing => _screenManager != null && _screenCapturer != null;

        public async void StartScreenSharing()
        {
            if (_manager == null || _screenManager != null || !VoipScreenCapture.IsSupported())
            {
                return;
            }

            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();

            if (item == null || _manager == null || _screenManager != null)
            {
                return;
            }

            _screenCapturer = new VoipScreenCapture(item);
            _screenCapturer.FatalErrorOccurred += OnFatalErrorOccurred;

            // TODO: currently Paused is triggered when frames are dropped as well.
            // This can happen because of high resource usage or even optimization (no changes on the screen => no frames available)
            //_screenDebouncer = new EventDebouncer<bool>(500,
            //    handler => _screenCapturer.Paused += new TypedEventHandler<VoipScreenCapture, bool>(handler),
            //    handler => _screenCapturer.Paused -= new TypedEventHandler<VoipScreenCapture, bool>(handler), true);
            //_screenDebouncer.Invoked += OnPaused;

            var descriptor = new VoipGroupDescriptor
            {
                VideoContentType = VoipVideoContentType.Screencast,
                VideoCapture = _screenCapturer
            };

            _screenManager = new VoipGroupManager(descriptor);

            RejoinScreenSharing();
        }

        private void OnPaused(object sender, bool paused)
        {
            ClientService.SendAsync(new ToggleGroupCallScreenSharingIsPaused(Id, paused));
        }

        private void OnFatalErrorOccurred(VoipCaptureBase sender, object args)
        {
            EndScreenSharing();
        }

        private void RejoinScreenSharing()
        {
            _screenManager?.SetConnectionMode(VoipGroupConnectionMode.None, false, IsRtmpStream);
            _screenManager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                var response = await ClientService.SendAsync(new StartGroupCallScreenSharing(Id, ssrc, payload));
                if (response is Text json)
                {
                    if (_screenManager == null)
                    {
                        return;
                    }

                    _screenSource = ssrc;
                    _screenManager.SetConnectionMode(VoipGroupConnectionMode.Rtc, true, IsRtmpStream);
                    _screenManager.SetJoinResponsePayload(json.TextValue);
                }
            });
        }

        public void EndScreenSharing()
        {
            if (_screenManager != null)
            {
                _screenManager.SetVideoCapture(null);

                _screenManager.Stop();
                _screenManager = null;

                _screenSource = 0;
            }

            if (_screenCapturer != null)
            {
                //_screenDebouncer.Invoked -= OnPaused;
                //_screenDebouncer = null;

                //_screenCapturer.SetOutput(null);
                _screenCapturer.FatalErrorOccurred -= OnFatalErrorOccurred;

                _screenCapturer.Stop();
                _screenCapturer = null;
            }

            ClientService.Send(new EndGroupCallScreenSharing(Id));
        }

        #endregion


        public void SetRequestedVideoChannels(IList<VoipVideoChannelInfo> descriptions)
        {
            _manager?.SetRequestedVideoChannels(descriptions);
        }

        public void AddIncomingVideoOutput(string endpointId, VoipVideoOutputSink sink)
        {
            _manager?.AddIncomingVideoOutput(endpointId, sink);
        }

        public void AddScreenSharingVideoOutput(string endpointId, VoipVideoOutputSink sink)
        {
            _screenManager?.AddIncomingVideoOutput(endpointId, sink);
        }

        private async void OnBroadcastTimeRequested(VoipGroupManager sender, BroadcastTimeRequestedEventArgs args)
        {
            if (IsRtmpStream)
            {
                var response = await ClientService.SendAsync(new GetGroupCallStreams(Id));
                if (response is GroupCallStreams streams && streams.Streams.Count > 0)
                {
                    args.Deferral(streams.Streams[0].TimeOffset);

                    if (_availableStreamsCount == 0)
                    {
                        _availableStreamsCount = 1;
                        AvailableStreamsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    args.Deferral(0);

                    if (_availableStreamsCount > 0)
                    {
                        _availableStreamsCount = 0;
                        AvailableStreamsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            else
            {
                var now = DateTime.Now + _timeDifference;
                var stamp = now.ToTimestampMilliseconds();

                args.Deferral(stamp);
            }
        }

        private async void OnBroadcastPartRequested(VoipGroupManager sender, BroadcastPartRequestedEventArgs args)
        {
            var now = DateTime.Now + _timeDifference;
            var stamp = now.ToTimestampMilliseconds();

            var time = args.Time;
            if (time == 0)
            {
                time = stamp;
            }

            var test = args.VideoQuality;

            var response = await ClientService.SendAsync(new GetGroupCallStreamSegment(Id, time, args.Scale, args.ChannelId, args.VideoQuality));

            now = DateTime.Now + _timeDifference;
            stamp = now.ToTimestamp();

            args.Deferral(time, stamp, response as FilePart);
        }

        private void OnNetworkStateUpdated(VoipGroupManager sender, GroupNetworkStateChangedEventArgs args)
        {
            //if (_isConnected && !connected)
            //{
            //    _connectingTimer.Change(5000, Timeout.Infinite);
            //}
            //else
            //{
            //    _connectingTimer.Change(Timeout.Infinite, Timeout.Infinite);
            //}

            _isConnected = args.IsConnected;
            NetworkStateChanged?.Invoke(this, new VoipGroupCallNetworkStateChangedEventArgs(args.IsConnected, args.IsTransitioningFromBroadcastToRtc));
        }

        private Dictionary<int, SpeakingParticipant> _speakingParticipants = new();

        private readonly struct SpeakingParticipant
        {
            public readonly ulong Timestamp;
            public readonly float Level;

            public SpeakingParticipant(ulong timestamp, float level)
            {
                Timestamp = timestamp;
                Level = level;
            }
        }

        private void OnAudioLevelsUpdated(VoipGroupManager sender, IList<VoipGroupParticipant> levels)
        {
            const float speakingLevelThreshold = 0.1f;
            const int cutoffTimeout = 3000;
            const int silentTimeout = 2000;

            var timestamp = Logger.TickCount;

            Dictionary<int, SpeakingParticipant> validSpeakers = null;
            HashSet<int> silentParticipants = new();

            foreach (var level in levels)
            {
                if (level.Level > speakingLevelThreshold && level.IsSpeaking)
                {
                    validSpeakers ??= new();
                    validSpeakers[level.AudioSource] = new SpeakingParticipant(timestamp, level.Level);
                }
                else
                {
                    silentParticipants.Add(level.AudioSource);
                }
            }

            foreach (var item in _speakingParticipants)
            {
                if (validSpeakers != null && validSpeakers.ContainsKey(item.Key))
                {
                    continue;
                }

                var delta = timestamp - item.Value.Timestamp;

                if (silentParticipants != null && silentParticipants.Contains(item.Key))
                {
                    if (delta < silentTimeout)
                    {
                        validSpeakers ??= new();
                        validSpeakers[item.Key] = item.Value;
                    }
                }
                else if (delta < cutoffTimeout)
                {
                    validSpeakers ??= new();
                    validSpeakers[item.Key] = item.Value;
                }
            }

            foreach (var item in levels)
            {
                ClientService.Send(new SetGroupCallParticipantIsSpeaking(Id, item.AudioSource, validSpeakers != null && validSpeakers.ContainsKey(item.AudioSource)));
            }

            if (validSpeakers != null)
            {
                _speakingParticipants = validSpeakers;
            }
            else
            {
                _speakingParticipants.Clear();
            }

            AudioLevelsUpdated?.Invoke(this, levels);
        }

        public override void Discard()
        {
            Discard(false);
        }

        public void Discard(bool end)
        {
            if (ScheduledStartDate > 0)
            {
                ThreadPool.QueueUserWorkItem(state => Aggregator.Publish(new UpdateGroupCall(new GroupCall(Id, Title, ScheduledStartDate, EnabledStartNotification, IsActive, IsRtmpStream, false, false, CanBeManaged, ParticipantCount, HasHiddenListeners, LoadedAllParticipants, RecentSpeakers, IsMyVideoEnabled, IsMyVideoPaused, CanEnableVideo2, MuteNewParticipants, CanToggleMuteNewParticipants, RecordDuration, IsVideoRecorded, Duration))));
            }
            else if (end)
            {
                ClientService.Send(new EndGroupCall(Id));
            }
            else
            {
                ClientService.Send(new LeaveGroupCall(Id));
            }
        }

        private void Dispose()
        {
            //_call = null;
            _chat = null;

            _isScheduled = false;
            _isConnected = false;
            _isClosed = true;

            _alias = null;
            _availableAliases = null;
            _availableAliasesTask?.TrySetResult(null);

            Participants?.Dispose();
            Participants = null;

            _devices.Changed -= OnDeviceChanged;
            _devices.Stop();

            if (_manager != null)
            {
                _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
                _manager.BroadcastTimeRequested -= OnBroadcastTimeRequested;
                _manager.BroadcastPartRequested -= OnBroadcastPartRequested;

                _manager.SetVideoCapture(null);

                _manager.Stop();
                _manager = null;

                _source = 0;
            }

            if (_capturer != null)
            {
                _capturer.SetOutput(null);

                _capturer.Stop();
                _capturer = null;
            }

            EndScreenSharing();

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

        public MediaDeviceTracker Devices => _devices;

        private string _videoInputId = string.Empty;
        public override string VideoInputId
        {
            get => _videoInputId;
            set
            {
                _devices.Track(MediaDeviceClass.VideoInput, value);
                _capturer?.SwitchToDevice(_videoInputId = value);
            }
        }

        private string _audioInputId = string.Empty;
        public override string AudioInputId
        {
            get => _audioInputId;
            set
            {
                _devices.Track(MediaDeviceClass.AudioInput, value);
                _manager?.SetAudioInputDevice(_audioInputId = value);
            }
        }

        private string _audioOutputId = string.Empty;
        public override string AudioOutputId
        {
            get => _audioOutputId;
            set
            {
                _devices.Track(MediaDeviceClass.AudioOutput, value);
                _manager?.SetAudioOutputDevice(_audioOutputId = value);
            }
        }

        private void OnDeviceChanged(object sender, MediaDeviceChangedEventArgs e)
        {
            switch (e.DeviceClass)
            {
                case MediaDeviceClass.VideoInput:
                    _capturer?.SwitchToDevice(_videoInputId = e.DeviceId);
                    break;
                case MediaDeviceClass.AudioInput:
                    _manager?.SetAudioInputDevice(_audioInputId = e.DeviceId);
                    break;
                case MediaDeviceClass.AudioOutput:
                    _manager?.SetAudioOutputDevice(_audioOutputId = e.DeviceId);
                    break;
            }
        }

        public bool IsMuted
        {
            get => _manager?.IsMuted ?? true;
            set
            {
                if (_manager != null && _currentUser != null && _manager.IsMuted != value)
                {
                    _manager.IsMuted = value;
                    ClientService.Send(new ToggleGroupCallParticipantIsMuted(Id, _currentUser.ParticipantId, value));
                    MutedChanged?.Invoke(this, EventArgs.Empty);

                    _coordinator?.TryNotifyMutedChanged(value);
                }
                else
                {
                    _coordinator?.TryNotifyMutedChanged(true);
                }
            }
        }

        public event TypedEventHandler<VoipGroupCall, EventArgs> MutedChanged;
        public event TypedEventHandler<VoipGroupCall, IList<VoipGroupParticipant>> AudioLevelsUpdated;

        public bool IsNoiseSuppressionEnabled
        {
            get => _manager?.IsNoiseSuppressionEnabled ?? false;
            set
            {
                if (_manager != null)
                {
                    _manager.IsNoiseSuppressionEnabled = value;
                }

                Settings.VoIP.IsNoiseSuppressionEnabled = value;
            }
        }

        public bool IsClosed => _isClosed;

        public void Update(GroupCall call, out bool closed)
        {
            closed = false;

            var isJoined = IsJoined;
            var needRejoin = NeedRejoin;

            Duration = call.Duration;
            IsVideoRecorded = call.IsVideoRecorded;
            RecordDuration = call.RecordDuration;
            CanToggleMuteNewParticipants = call.CanToggleMuteNewParticipants;
            MuteNewParticipants = call.MuteNewParticipants;
            CanEnableVideo2 = call.CanEnableVideo;
            IsMyVideoPaused = call.IsMyVideoPaused;
            IsMyVideoEnabled = call.IsMyVideoEnabled;
            RecentSpeakers = call.RecentSpeakers;
            LoadedAllParticipants = call.LoadedAllParticipants;
            HasHiddenListeners = call.HasHiddenListeners;
            ParticipantCount = call.ParticipantCount;
            CanBeManaged = call.CanBeManaged;
            NeedRejoin = call.NeedRejoin;
            IsJoined = call.IsJoined;
            IsRtmpStream = call.IsRtmpStream;
            IsActive = call.IsActive;
            EnabledStartNotification = call.EnabledStartNotification;
            ScheduledStartDate = call.ScheduledStartDate;
            Title = call.Title;
            Id = call.Id;

            if (call.IsJoined || call.NeedRejoin || _isScheduled != (call.ScheduledStartDate > 0))
            {
                if (call.NeedRejoin || _isScheduled != (call.ScheduledStartDate > 0))
                {
                    Rejoin(_alias);
                }

                _isScheduled = call.ScheduledStartDate > 0;
            }
            else if (call.IsJoined != (isJoined || needRejoin))
            {
                Dispose();
                Discard();
                closed = true;
            }

            if (isJoined != IsJoined || needRejoin != NeedRejoin)
            {
                JoinedStateChanged?.Invoke(this, new VoipGroupCallJoinedStateChangedEventArgs(IsJoined, NeedRejoin));
            }

            RaisePropertyChanged(nameof(Call));
        }

        public void Update(GroupCallParticipant participant)
        {
            Participants?.Update(participant);

            if (participant.IsCurrentUser)
            {
                // User got muted by admins, update local state
                if (participant.IsMutedForAllUsers && !participant.CanUnmuteSelf)
                {
                    _manager.IsMuted = true;

                    if (_currentUser?.VideoInfo != null && participant.VideoInfo == null && _capturer != null)
                    {
                        _capturer.SetOutput(null);
                        _manager.SetVideoCapture(null);

                        _capturer.Stop();
                        _capturer = null;
                    }

                    if (_currentUser?.ScreenSharingVideoInfo != null && participant.ScreenSharingVideoInfo == null && _screenCapturer != null)
                    {
                        EndScreenSharing();
                    }
                }
                else if (_currentUser?.ScreenSharingVideoInfo != null && participant.ScreenSharingVideoInfo == null && _screenCapturer != null)
                {
                    RejoinScreenSharing();
                }

                _currentUser = participant;
                RaisePropertyChanged(nameof(CurrentUser));
            }

            var manager = _manager;
            if (manager == null)
            {
                return;
            }

            if (participant.IsMutedForCurrentUser)
            {
                manager.SetVolume(participant.AudioSourceId, 0);
            }
            else
            {
                manager.SetVolume(participant.AudioSourceId, participant.VolumeLevel / 10000d);
            }
        }

        public string GetTitle()
        {
            if (string.IsNullOrEmpty(Title) && ClientService.TryGetChat(Chat.Id, out Chat chat))
            {
                return chat.Title;
            }

            return Title;
        }

        public Chat Chat => _chat;
        public bool IsChannel => IsRtmpStream is true || (_chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        //public GroupCall Call => _call;
        public GroupCallParticipant CurrentUser => _currentUser;

        /// <summary>
        /// Call duration, in seconds; for ended calls only.
        /// </summary>
        public int Duration { get; private set; }

        /// <summary>
        /// True, if a video file is being recorded for the call.
        /// </summary>
        public bool IsVideoRecorded { get; private set; }

        /// <summary>
        /// Duration of the ongoing group call recording, in seconds; 0 if none. An updateGroupCall
        /// update is not triggered when value of this field changes, but the same recording
        /// goes on.
        /// </summary>
        public int RecordDuration { get; private set; }

        /// <summary>
        /// True, if the current user can enable or disable MuteNewParticipants setting.
        /// </summary>
        public bool CanToggleMuteNewParticipants { get; private set; }

        /// <summary>
        /// True, if only group call administrators can unmute new participants.
        /// </summary>
        public bool MuteNewParticipants { get; private set; }

        /// <summary>
        /// True, if the current user can broadcast video or share screen.
        /// </summary>
        public bool CanEnableVideo2 { get; private set; }

        /// <summary>
        /// True, if the current user's video is paused.
        /// </summary>
        public bool IsMyVideoPaused { get; private set; }

        /// <summary>
        /// True, if the current user's video is enabled.
        /// </summary>
        public bool IsMyVideoEnabled { get; private set; }

        /// <summary>
        /// At most 3 recently speaking users in the group call.
        /// </summary>
        public IList<GroupCallRecentSpeaker> RecentSpeakers { get; private set; }

        /// <summary>
        /// True, if all group call participants are loaded.
        /// </summary>
        public bool LoadedAllParticipants { get; private set; }

        /// <summary>
        /// True, if group call participants, which are muted, aren't returned in participant
        /// list.
        /// </summary>
        public bool HasHiddenListeners { get; private set; }

        /// <summary>
        /// Number of participants in the group call.
        /// </summary>
        public int ParticipantCount { get; private set; }

        /// <summary>
        /// True, if the current user can manage the group call.
        /// </summary>
        public bool CanBeManaged { get; private set; }

        /// <summary>
        /// True, if user was kicked from the call because of network loss and the call needs
        /// to be rejoined.
        /// </summary>
        public bool NeedRejoin { get; private set; }

        /// <summary>
        /// True, if the call is joined.
        /// </summary>
        public bool IsJoined { get; private set; }

        /// <summary>
        /// True, if the chat is an RTMP stream instead of an ordinary video chat.
        /// </summary>
        public bool IsRtmpStream { get; private set; }

        /// <summary>
        /// True, if the call is active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// True, if the group call is scheduled and the current user will receive a notification
        /// when the group call starts.
        /// </summary>
        public bool EnabledStartNotification { get; private set; }

        /// <summary>
        /// Point in time (Unix timestamp) when the group call is expected to be started
        /// by an administrator; 0 if it is already active or was ended.
        /// </summary>
        public int ScheduledStartDate { get; private set; }

        /// <summary>
        /// Group call title.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Group call identifier.
        /// </summary>
        public int Id { get; private set; }


        public int Source => _source;

        public bool IsConnected => _isConnected;

        private void CreateWindow()
        {
            var service = TypeResolver.Current.Resolve<IViewService>(int.MaxValue);
            var options = new ViewServiceOptions
            {
                Width = 720,
                Height = 540,
                PersistedId = IsRtmpStream ? "LiveStream" : "VideoChat",
                Content = CreatePresentation,
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
                //if (IsVideo && _camera == null)
                //{
                //    _camera = new VoipVideoCapture(_videoInputId);
                //    _camera.FatalErrorOccurred += OnFatalErrorOccurred;

                //    _videoState = VoipVideoState.Active;
                //}
            }

            return IsRtmpStream
                ? new LiveStreamPage(this)
                : new GroupCallPage(this);
        }

        public override void Show/*Window*/()
        {
            WindowContext.Activate(IsRtmpStream ? "LiveStream" : "VideoChat");
        }

        public async Task ConsolidateAsync()
        {
            //using (await _updateLock.WaitAsync())
            //{
            //    //Aggregator.Publish(new UpdateCallDialog(_call, true));

            //    var lifetime = _lifetime;
            //    if (lifetime != null)
            //    {
            //        _lifetime = null;
            //        await lifetime.ConsolidateAsync();
            //    }
            //}
        }
    }
}
