//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services.Updates;
using Unigram.Services.ViewService;
using Unigram.ViewModels;
using Unigram.Views.Calls;
using Unigram.Views.Popups;
using Windows.ApplicationModel.Calls;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.System.Display;
using Windows.UI.ViewManagement;

namespace Unigram.Services
{
    public interface IGroupCallService : INotifyPropertyChanged
    {
        event EventHandler AvailableStreamsChanged;
        int AvailableStreamsCount { get; }

        IClientService ClientService { get; }

        string CurrentVideoInput { get; set; }
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }

#if ENABLE_CALLS
        bool IsMuted { get; set; }
        event EventHandler MutedChanged;

        bool IsNoiseSuppressionEnabled { get; set; }

        VoipGroupManager Manager { get; }
#endif

        GroupCallParticipantsCollection Participants { get; }

        Chat Chat { get; }
        bool IsChannel { get; }

        GroupCall Call { get; }
        GroupCallParticipant CurrentUser { get; }

#if ENABLE_CALLS
        int Source { get; }
#endif

        bool IsConnected { get; }

        Task ShowAsync();

        Task<bool> CanChooseAliasAsync(long chatId);

        Task JoinAsync(XamlRoot xamlRoot, long chatId);
        Task RejoinAsync(XamlRoot xamlRoot);
        Task LeaveAsync();

        Task CreateAsync(XamlRoot xamlRoot, long chatId);
        Task DiscardAsync();

#if ENABLE_CALLS
        bool CanEnableVideo { get; }
        bool IsVideoEnabled { get; }
        void ToggleCapturing();

        VoipGroupManager ScreenSharing { get; }

        bool IsScreenSharing { get; }
        void StartScreenSharing();
        void EndScreenSharing();
#endif

        Task ConsolidateAsync();
    }

    public class GroupCallService : TLViewModelBase
        , IGroupCallService
    //, IHandle<UpdateGroupCall>
    //, IHandle<UpdateGroupCallParticipant>
    {
        private readonly IViewService _viewService;

        private readonly MediaDeviceWatcher _videoWatcher;

        private readonly MediaDeviceWatcher _inputWatcher;
        private readonly MediaDeviceWatcher _outputWatcher;

        private readonly DisposableMutex _updateLock = new DisposableMutex();

        private Chat _chat;

        private MessageSender _alias;
        private MessageSenders _availableAliases;
        private TaskCompletionSource<MessageSenders> _availableAliasesTask;

        private GroupCall _call;
        private GroupCallParticipant _currentUser;

        private TimeSpan _timeDifference;

#if ENABLE_CALLS
        private VoipGroupManager _manager;
        private VoipVideoCapture _capturer;
        private int _source;

        private VoipGroupManager _screenManager;
        private VoipScreenCapture _screenCapturer;
        private EventDebouncer<bool> _screenDebouncer;
        private int _screenSource;

        private VoipCallCoordinator _coordinator;
        private VoipPhoneCall _systemCall;
#endif

        private bool _isScheduled;
        private bool _isConnected;
        private bool _isJoining;

        private ViewLifetimeControl _lifetime;

        private DisplayRequest _request;

        private int _availableStreamsCount = 0;

        public GroupCallService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(clientService, settingsService, aggregator)
        {
            _viewService = viewService;

#if ENABLE_CALLS
            _videoWatcher = new MediaDeviceWatcher(DeviceClass.VideoCapture, id => _capturer?.SwitchToDevice(id));
            _inputWatcher = new MediaDeviceWatcher(DeviceClass.AudioCapture, id => _manager?.SetAudioInputDevice(id));
            _outputWatcher = new MediaDeviceWatcher(DeviceClass.AudioRender, id => _manager?.SetAudioOutputDevice(id));
#endif

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateGroupCall>(this, Handle)
                .Subscribe<UpdateGroupCallParticipant>(Handle);
        }

        public event EventHandler AvailableStreamsChanged;
        public int AvailableStreamsCount => _availableStreamsCount;

#if ENABLE_CALLS
        public VoipGroupManager Manager => _manager;
#endif

        private GroupCallParticipantsCollection _participants;
        public GroupCallParticipantsCollection Participants
        {
            get => _participants;
            private set => Set(ref _participants, value);
        }

        public async Task<bool> CanChooseAliasAsync(long chatId)
        {
            var aliases = _availableAliases;
            if (aliases != null)
            {
                return aliases.TotalCount > 1;
            }

            var tsc = _availableAliasesTask;
            if (tsc != null)
            {
                var response = await tsc.Task;
                return response?.TotalCount > 1;
            }

            tsc = _availableAliasesTask = new TaskCompletionSource<MessageSenders>();

            var result = await CanChooseAliasAsyncInternal(tsc, chatId);
            tsc.TrySetResult(result);

            return result?.TotalCount > 1;
        }

        private async Task<MessageSenders> CanChooseAliasAsyncInternal(TaskCompletionSource<MessageSenders> tsc, long chatId)
        {
            var response = await ClientService.SendAsync(new GetVideoChatAvailableParticipants(chatId));
            if (response is MessageSenders senders)
            {
                _availableAliases = senders;
                tsc.TrySetResult(senders);
                return senders;
            }

            return null;
        }


        public async Task JoinAsync(XamlRoot xamlRoot, long chatId)
        {
            var chat = ClientService.GetChat(chatId);
            if (chat == null || chat.VideoChat.GroupCallId == 0)
            {
                return;
            }

            //var call = Call;
            //if (call != null)
            //{
            //    var callUser = ClientService.GetUser(call.UserId);
            //    if (callUser != null && callUser.Id != user.Id)
            //    {
            //        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.VoipOngoingAlert, callUser.GetFullName(), user.GetFullName()), Strings.Resources.VoipOngoingAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
            //        if (confirm == ContentDialogResult.Primary)
            //        {

            //        }
            //    }
            //    else
            //    {
            //        Show();
            //    }

            //    return;
            //}

            var permissions = await MediaDeviceWatcher.CheckAccessAsync(xamlRoot, true, false);
            if (permissions == false)
            {
                return;
            }

            await JoinAsyncInternal(xamlRoot, chat, chat.VideoChat.GroupCallId, null);
        }

        public Task LeaveAsync()
        {
            return DisposeAsync(false);
        }

        public async Task CreateAsync(XamlRoot xamlRoot, long chatId)
        {
            var chat = ClientService.GetChat(chatId);
            if (chat == null || chat.VideoChat.GroupCallId != 0)
            {
                return;
            }

            await CanChooseAliasAsync(chat.Id);

            var popup = new VideoChatAliasesPopup(ClientService, chat, true, _availableAliases?.Senders.ToArray());

            var confirm = await popup.ShowQueuedAsync(xamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                var participantId = popup.SelectedSender ?? new MessageSenderUser(ClientService.Options.MyId);
                var startDate = 0;

                if (popup.IsScheduleSelected)
                {
                    var schedule = new ScheduleVideoChatPopup(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel);

                    var again = await schedule.ShowQueuedAsync(xamlRoot);
                    if (again != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    startDate = schedule.Value.ToTimestamp();
                }
                else if (popup.IsStartWithSelected)
                {
                    var streams = new VideoChatStreamsPopup(ClientService, chat.Id, true);

                    var again = await streams.ShowQueuedAsync(xamlRoot);
                    if (again != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    if (streams.IsScheduleSelected)
                    {
                        var schedule = new ScheduleVideoChatPopup(true);

                        var oneMore = await schedule.ShowQueuedAsync(xamlRoot);
                        if (oneMore != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        startDate = schedule.Value.ToTimestamp();
                    }
                }

                var response = await ClientService.SendAsync(new CreateVideoChat(chat.Id, string.Empty, startDate, popup.IsStartWithSelected));
                if (response is GroupCallId groupCallId)
                {
                    await JoinAsyncInternal(xamlRoot, chat, groupCallId.Id, participantId);
                }
            }
        }

        private async Task JoinAsyncInternal(XamlRoot xamlRoot, Chat chat, int groupCallId, MessageSender alias)
        {
            var activeCall = _call;
            if (activeCall != null)
            {
                if (activeCall.Id == groupCallId)
                {
                    await ShowAsync();
                    return;
                }
                else
                {
                    var confirm = await MessagePopup.ShowAsync(xamlRoot, string.Format(Strings.Resources.VoipOngoingChatAlert, _chat.Title, chat.Title), Strings.Resources.VoipOngoingChatAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    await DisposeAsync(false);
                }
            }

            alias ??= chat.VideoChat.DefaultParticipantId;

            if (alias == null)
            {
                alias = await PickAliasAsync(xamlRoot, chat, false);
            }

            var response = await ClientService.SendAsync(new GetGroupCall(groupCallId));
            if (response is GroupCall groupCall)
            {
                var unix = await ClientService.SendAsync(new GetOption("unix_time")) as OptionValueInteger;
                if (unix == null)
                {
                    return;
                }

                _timeDifference = DateTime.Now - Utils.UnixTimestampToDateTime(unix.Value);

                _chat = chat;
                _call = groupCall;

                _isScheduled = groupCall.ScheduledStartDate > 0;

#if ENABLE_CALLS
                await InitializeSystemCallAsync(chat);

                var descriptor = new VoipGroupDescriptor
                {
                    AudioInputId = await _inputWatcher.GetAndUpdateAsync(),
                    AudioOutputId = await _outputWatcher.GetAndUpdateAsync(),
                    IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled
                };

                _inputWatcher.Start();
                _outputWatcher.Start();

                _manager = new VoipGroupManager(descriptor);
                _manager.NetworkStateUpdated += OnNetworkStateUpdated;
                _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
                _manager.BroadcastTimeRequested += OnBroadcastTimeRequested;
                _manager.BroadcastPartRequested += OnBroadcastPartRequested;

                _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);
#endif

                // This must be set before, as updates might come
                // between ShowAsync and Rejoin.
                _isJoining = true;

                await ShowAsync();

                if (groupCall.ScheduledStartDate > 0)
                {
                    ClientService.Send(new SetVideoChatDefaultParticipant(chat.Id, alias));
                }
                else
                {
                    RequestActive();
                    Rejoin(groupCall, alias);
                }
            }
        }

        private async Task InitializeSystemCallAsync(Chat chat)
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

                    // I'm not sure if RequestNewOutgoingCall is the right method to call, but it seem to work.
                    _systemCall = _coordinator.RequestNewOutgoingCall($"{chat.Id}", chat.Title, Strings.Resources.AppName, VoipPhoneCallMedia.Audio | VoipPhoneCallMedia.Video);
                    _systemCall.TryNotifyCallActive();
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

        private async void OnEndRequested(VoipPhoneCall sender, CallStateChangeEventArgs args)
        {
            await LeaveAsync();
        }

        public async Task RejoinAsync(XamlRoot xamlRoot)
        {
            var call = _call;
            var chat = _chat;

            if (call == null || chat == null)
            {
                return;
            }

            var alias = await PickAliasAsync(xamlRoot, chat, true);
            if (alias != null)
            {
                Rejoin(call, alias);
            }
        }

        private async Task<MessageSender> PickAliasAsync(XamlRoot xamlRoot, Chat chat, bool darkTheme)
        {
            var available = await CanChooseAliasAsync(chat.Id);
            if (available && _availableAliases != null)
            {
                var popup = new VideoChatAliasesPopup(ClientService, chat, false, _availableAliases.Senders.ToArray());
                popup.RequestedTheme = darkTheme ? ElementTheme.Dark : ElementTheme.Default;

                var confirm = await popup.ShowQueuedAsync(xamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    return popup.SelectedSender;
                }
            }

            return null;
        }

        private void Rejoin(GroupCall groupCall, MessageSender alias)
        {
            _isJoining = true;
            _alias = alias;

#if ENABLE_CALLS
            _manager?.SetConnectionMode(VoipGroupConnectionMode.None, false, groupCall.IsRtmpStream);
            _manager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                if (groupCall.IsRtmpStream)
                {
                    Participants = null;
                }
                else
                {
                    Participants ??= new GroupCallParticipantsCollection(ClientService, Aggregator, groupCall);
                }

                var response = await ClientService.SendAsync(new JoinGroupCall(groupCall.Id, alias, ssrc, payload, _manager.IsMuted, _capturer != null, string.Empty));
                if (response is Text json)
                {
                    _isJoining = false;

                    if (_manager == null)
                    {
                        return;
                    }

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
                    _manager.SetConnectionMode(broadcast ? VoipGroupConnectionMode.Broadcast : VoipGroupConnectionMode.Rtc, true, groupCall.IsRtmpStream);
                    _manager.SetJoinResponsePayload(json.TextValue);

                    RejoinScreenSharing(groupCall);
                }
            });
#endif
        }

        #region Capturing
#if ENABLE_CALLS
        public bool CanEnableVideo
        {
            get
            {
                var call = _call;
                if (call != null && call.CanEnableVideo)
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

        public async void ToggleCapturing()
        {
            var call = _call;
            if (call == null || _manager == null)
            {
                return;
            }

            if (_capturer != null)
            {
                _capturer.SetOutput(null);
                _manager.SetVideoCapture(null);

                _capturer.Dispose();
                _capturer = null;
            }
            else
            {
                _capturer = new VoipVideoCapture(await _videoWatcher.GetAndUpdateAsync());
                _manager.SetVideoCapture(_capturer);
            }

            ClientService.Send(new ToggleGroupCallIsMyVideoEnabled(call.Id, _capturer != null));
        }
#endif
        #endregion

        #region Screencast
#if ENABLE_CALLS
        public VoipGroupManager ScreenSharing => _screenManager;
        public bool IsScreenSharing => _screenManager != null && _screenCapturer != null;

        public async void StartScreenSharing()
        {
            var call = _call;
            if (call == null || _manager == null || _screenManager != null || !GraphicsCaptureSession.IsSupported())
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

            _screenDebouncer = new EventDebouncer<bool>(500,
                handler => _screenCapturer.Paused += new TypedEventHandler<VoipScreenCapture, bool>(handler),
                handler => _screenCapturer.Paused -= new TypedEventHandler<VoipScreenCapture, bool>(handler), true);
            _screenDebouncer.Invoked += OnPaused;

            var descriptor = new VoipGroupDescriptor
            {
                VideoContentType = VoipVideoContentType.Screencast,
                VideoCapture = _screenCapturer
            };

            _screenManager = new VoipGroupManager(descriptor);

            RejoinScreenSharing(call);
        }

        private void OnPaused(object sender, bool paused)
        {
            var call = _call;
            if (call != null)
            {
                ClientService.SendAsync(new ToggleGroupCallScreenSharingIsPaused(call.Id, paused));
            }
        }

        private void OnFatalErrorOccurred(VoipScreenCapture sender, object args)
        {
            EndScreenSharing();
        }

        private void RejoinScreenSharing(GroupCall groupCall)
        {
            _screenManager?.SetConnectionMode(VoipGroupConnectionMode.None, false, groupCall.IsRtmpStream);
            _screenManager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                var response = await ClientService.SendAsync(new StartGroupCallScreenSharing(groupCall.Id, ssrc, payload));
                if (response is Text json)
                {
                    if (_screenManager == null)
                    {
                        return;
                    }

                    _screenSource = ssrc;
                    _screenManager.SetConnectionMode(VoipGroupConnectionMode.Rtc, true, groupCall.IsRtmpStream);
                    _screenManager.SetJoinResponsePayload(json.TextValue);
                }
            });
        }

        public void EndScreenSharing()
        {
            if (_screenManager != null)
            {
                _screenManager.SetVideoCapture(null);

                _screenManager.Dispose();
                _screenManager = null;

                _screenSource = 0;
            }

            if (_screenCapturer != null)
            {
                _screenDebouncer.Invoked -= OnPaused;
                _screenDebouncer = null;

                //_screenCapturer.SetOutput(null);
                _screenCapturer.FatalErrorOccurred -= OnFatalErrorOccurred;
                _screenCapturer.Dispose();
                _screenCapturer = null;
            }

            var call = _call;
            if (call != null)
            {
                ClientService.Send(new EndGroupCallScreenSharing(call.Id));
            }
        }
#endif
        #endregion

#if ENABLE_CALLS

        private async void OnBroadcastTimeRequested(VoipGroupManager sender, BroadcastTimeRequestedEventArgs args)
        {
            var call = _call;
            if (call == null)
            {
                return;
            }

            if (call.IsRtmpStream)
            {
                var response = await ClientService.SendAsync(new GetGroupCallStreams(call.Id));
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
            var call = _call;
            if (call == null)
            {
                return;
            }

            var now = DateTime.Now + _timeDifference;
            var stamp = now.ToTimestampMilliseconds();

            var time = args.Time;
            if (time == 0)
            {
                time = stamp;
            }

            var test = args.VideoQuality;

            var response = await ClientService.SendAsync(new GetGroupCallStreamSegment(call.Id, time, args.Scale, args.ChannelId, args.VideoQuality));

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
        }

        private Dictionary<int, SpeakingParticipant> _speakingParticipants = new();

        private readonly struct SpeakingParticipant
        {
            public readonly int Timestamp;
            public readonly float Level;

            public SpeakingParticipant(int timestamp, float level)
            {
                Timestamp = timestamp;
                Level = level;
            }
        }

        private void OnAudioLevelsUpdated(VoipGroupManager sender, IReadOnlyDictionary<int, KeyValuePair<float, bool>> levels)
        {
            var call = _call;
            if (call == null)
            {
                return;
            }

            const float speakingLevelThreshold = 0.1f;
            const int cutoffTimeout = 3000;
            const int silentTimeout = 2000;

            var timestamp = Environment.TickCount;

            var validSpeakers = new Dictionary<int, SpeakingParticipant>();
            var silentParticipants = new HashSet<int>();

            foreach (var level in levels)
            {
                if (level.Value.Key > speakingLevelThreshold && level.Value.Value)
                {
                    validSpeakers[level.Key] = new SpeakingParticipant(timestamp, level.Value.Key);
                }
                else
                {
                    silentParticipants.Add(level.Key);
                }
            }

            foreach (var item in _speakingParticipants)
            {
                if (validSpeakers.ContainsKey(item.Key))
                {
                    continue;
                }

                var delta = timestamp - item.Value.Timestamp;

                if (silentParticipants.Contains(item.Key))
                {
                    if (delta < silentTimeout)
                    {
                        validSpeakers[item.Key] = item.Value;
                    }
                }
                else if (delta < cutoffTimeout)
                {
                    validSpeakers[item.Key] = item.Value;
                }
            }

            foreach (var item in levels)
            {
                ClientService.Send(new SetGroupCallParticipantIsSpeaking(call.Id, item.Key, validSpeakers.ContainsKey(item.Key)));
            }

            _speakingParticipants = validSpeakers;
        }

#endif

        public Task DiscardAsync()
        {
            return DisposeAsync(true);
        }

        private Task DisposeAsync(bool? discard = null)
        {
            if (_call != null && discard is true)
            {
                ClientService.Send(new EndGroupCall(_call.Id));
            }
            else if (_call != null && discard is false)
            {
                ClientService.Send(new LeaveGroupCall(_call.Id));
            }

            _call = null;
            _chat = null;

            _isScheduled = false;
            _isJoining = false;

            _alias = null;
            _availableAliases = null;
            _availableAliasesTask = null;

            Participants?.Dispose();
            Participants = null;

            RequestRelease();

#if ENABLE_CALLS
            return Task.Run(() =>
            {
                if (_manager != null)
                {
                    _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                    _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
                    _manager.BroadcastTimeRequested -= OnBroadcastTimeRequested;
                    _manager.BroadcastPartRequested -= OnBroadcastPartRequested;

                    _manager.SetVideoCapture(null);

                    _manager.Dispose();
                    _manager = null;

                    _source = 0;
                }

                if (_capturer != null)
                {
                    _capturer.SetOutput(null);
                    _capturer.Dispose();
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
            });
#else
            return Task.CompletedTask;
#endif
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

#if ENABLE_CALLS

        public bool IsMuted
        {
            get => _manager.IsMuted;
            set
            {
                if (_manager != null && _currentUser != null && _manager.IsMuted != value)
                {
                    _manager.IsMuted = value;
                    ClientService.Send(new ToggleGroupCallParticipantIsMuted(_call.Id, _currentUser.ParticipantId, value));
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

        public bool IsNoiseSuppressionEnabled
        {
            get => _manager.IsNoiseSuppressionEnabled;
            set => _manager.IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled = value;
        }

#endif

        public async void Handle(UpdateGroupCall update)
        {
#if ENABLE_CALLS
            if (_call?.Id == update.GroupCall.Id)
            {
                if (update.GroupCall.IsJoined || update.GroupCall.NeedRejoin || _isJoining)
                {
                    _call = update.GroupCall;

                    var lifetime = _lifetime;
                    if (lifetime != null)
                    {
                        lifetime.Dispatcher.Dispatch(() =>
                        {
                            if (lifetime.Window.Content is GroupCallPage callPage)
                            {
                                callPage.Update(update.GroupCall, _currentUser);
                            }
                        });
                    }

                    if (update.GroupCall.NeedRejoin || _isScheduled != (update.GroupCall.ScheduledStartDate > 0))
                    {
                        Rejoin(update.GroupCall, _alias);
                    }

                    _isScheduled = update.GroupCall.ScheduledStartDate > 0;
                }
                else
                {
                    await ConsolidateAsync();
                    await DisposeAsync();
                }
            }

            Aggregator.Publish(new UpdateCallDialog(update.GroupCall));
#endif
        }

        private void RequestActive()
        {
            if (_request == null)
            {
                if (CoreApplication.MainView.DispatcherQueue.HasThreadAccess)
                {
                    try
                    {
                        _request = new DisplayRequest();
                        _request.RequestActive();
                    }
                    catch { }
                }
                else
                {
                    CoreApplication.MainView.DispatcherQueue.TryEnqueue(RequestActive);
                }
            }
        }

        private void RequestRelease()
        {
            if (_request != null)
            {
                if (CoreApplication.MainView.DispatcherQueue.HasThreadAccess)
                {
                    try
                    {
                        _request.RequestRelease();
                        _request = null;
                    }
                    catch { }
                }
                else
                {
                    CoreApplication.MainView.DispatcherQueue.TryEnqueue(RequestRelease);
                }
            }
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
#if ENABLE_CALLS
            if (_call?.Id == update.GroupCallId && update.Participant.IsCurrentUser)
            {
                // User got muted by admins, update local state
                if (update.Participant.IsMutedForAllUsers && !update.Participant.CanUnmuteSelf)
                {
                    _manager.IsMuted = true;

                    if (_currentUser?.VideoInfo != null && update.Participant.VideoInfo == null && _capturer != null)
                    {
                        _capturer.SetOutput(null);
                        _manager.SetVideoCapture(null);

                        _capturer.Dispose();
                        _capturer = null;
                    }

                    if (_currentUser?.ScreenSharingVideoInfo != null && update.Participant.ScreenSharingVideoInfo == null && _screenCapturer != null)
                    {
                        EndScreenSharing();
                    }
                }
                else if (_currentUser?.ScreenSharingVideoInfo != null && update.Participant.ScreenSharingVideoInfo == null && _screenCapturer != null)
                {
                    RejoinScreenSharing(_call);
                }

                _currentUser = update.Participant;

                var lifetime = _lifetime;
                if (lifetime != null)
                {
                    lifetime.Dispatcher.Dispatch(() =>
                    {
                        if (lifetime.Window.Content is GroupCallPage callPage)
                        {
                            callPage.Update(_call, update.Participant);
                        }
                    });
                }
            }

            var manager = _manager;
            if (manager == null)
            {
                return;
            }

            if (update.Participant.IsMutedForCurrentUser)
            {
                manager.SetVolume(update.Participant.AudioSourceId, 0);
            }
            else
            {
                manager.SetVolume(update.Participant.AudioSourceId, update.Participant.VolumeLevel / 10000d);
            }
#endif
        }

        public Chat Chat => _chat;
        public bool IsChannel => _call?.IsRtmpStream is true || (_chat?.Type is ChatTypeSupergroup super && super.IsChannel);
        public GroupCall Call => _call;
        public GroupCallParticipant CurrentUser => _currentUser;

#if ENABLE_CALLS
        public int Source => _source;
#endif

        public bool IsConnected => _isConnected;

        public async Task ShowAsync()
        {
#if ENABLE_CALLS
            using (await _updateLock.WaitAsync())
            {
                if (_lifetime == null)
                {
                    var call = _call;
                    if (call == null)
                    {
                        return;
                    }

                    var parameters = new ViewServiceParams
                    {
                        Title = IsChannel ? Strings.Resources.VoipChannelVoiceChat : Strings.Resources.VoipGroupVoiceChat,
                        Width = call.IsRtmpStream ? 580 : 380,
                        Height = call.IsRtmpStream ? 380 : 580,
                        PersistentId = call.IsRtmpStream ? "LiveStream" : "VideoChat",
                        Content = call.IsRtmpStream
                            ? control => new LiveStreamPage(ClientService, Aggregator, this)
                            : control => new GroupCallPage(ClientService, Aggregator, this)
                    };

                    _lifetime = await _viewService.OpenAsync(parameters);
                    _lifetime.Closed += ApplicationView_Released;
                    _lifetime.Released += ApplicationView_Released;

                    //Aggregator.Publish(new UpdateCallDialog(call, true));
                }
                else
                {
                    await ApplicationViewSwitcher.SwitchAsync(_lifetime.Id);
                }
            }
#endif
        }

        public async Task ConsolidateAsync()
        {
            using (await _updateLock.WaitAsync())
            {
                //Aggregator.Publish(new UpdateCallDialog(_call, true));

                var lifetime = _lifetime;
                if (lifetime != null)
                {
                    _lifetime = null;
                    await lifetime.Dispatcher.DispatchAsync(() => ApplicationView.GetForCurrentView().ConsolidateAsync());
                }
            }
        }

        private async void ApplicationView_Released(object sender, EventArgs e)
        {
            using (await _updateLock.WaitAsync())
            {
                _lifetime = null;
                //Aggregator.Publish(new UpdateCallDialog(_call, false));
            }
        }
    }
}
