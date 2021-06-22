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
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.Data.Json;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IGroupCallService : INotifyPropertyChanged
    {
        ICacheService CacheService { get; }

        string CurrentVideoInput { get; set; }
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }

        bool IsNoiseSuppressionEnabled { get; set; }

        VoipGroupManager Manager { get; }

        GroupCallParticipantsCollection Participants { get; }

        Chat Chat { get; }

        GroupCall Call { get; }
        GroupCallParticipant CurrentUser { get; }

        int Source { get; }
        bool IsConnected { get; }

        Task ShowAsync();

        Task<bool> CanChooseAliasAsync(long chatId);

        Task JoinAsync(long chatId);
        Task RejoinAsync();
        Task LeaveAsync();

        Task CreateAsync(long chatId);
        Task DiscardAsync();

        bool CanEnableVideo { get; }
        bool IsVideoEnabled { get; }
        void ToggleCapturing();

        VoipGroupManager ScreenSharing { get; }

        bool IsScreenSharing { get; }
        void StartScreenSharing();
        void EndScreenSharing();

        Task ConsolidateAsync();
    }

    public class GroupCallService : TLViewModelBase, IGroupCallService, IHandle<UpdateGroupCall>, IHandle<UpdateGroupCallParticipant>
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

        private VoipGroupManager _manager;
        private VoipVideoCapture _capturer;
        private int _source;

        private VoipGroupManager _screenManager;
        private VoipScreenCapture _screenCapturer;
        private EventDebouncer<bool> _screenDebouncer;
        private int _screenSource;

        private bool _isScheduled;
        private bool _isConnected;
        private bool _isJoining;

        private ViewLifetimeControl _callLifetime;

        public GroupCallService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _viewService = viewService;

            _videoWatcher = new MediaDeviceWatcher(DeviceClass.VideoCapture, id => _capturer?.SwitchToDevice(id));
            _inputWatcher = new MediaDeviceWatcher(DeviceClass.AudioCapture, id => _manager?.SetAudioInputDevice(id));
            _outputWatcher = new MediaDeviceWatcher(DeviceClass.AudioRender, id => _manager?.SetAudioOutputDevice(id));

            aggregator.Subscribe(this);
        }

        public VoipGroupManager Manager => _manager;

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
            var response = await ProtoService.SendAsync(new GetVoiceChatAvailableParticipants(chatId));
            if (response is MessageSenders senders)
            {
                _availableAliases = senders;
                tsc.TrySetResult(senders);
                return senders;
            }

            return null;
        }


        public async Task JoinAsync(long chatId)
        {
            var chat = CacheService.GetChat(chatId);
            if (chat == null || chat.VoiceChat.GroupCallId == 0)
            {
                return;
            }

            //var call = Call;
            //if (call != null)
            //{
            //    var callUser = CacheService.GetUser(call.UserId);
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

            await JoinAsyncInternal(chat, chat.VoiceChat.GroupCallId, null);
        }

        public Task LeaveAsync()
        {
            return DisposeAsync(false);
        }

        public async Task CreateAsync(long chatId)
        {
            var chat = CacheService.GetChat(chatId);
            if (chat == null || chat.VoiceChat.GroupCallId != 0)
            {
                return;
            }

            await CanChooseAliasAsync(chat.Id);

            var popup = new VoiceChatAliasesPopup(ProtoService, chat, true, _availableAliases?.Senders.ToArray());

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var participantId = popup.SelectedSender ?? new MessageSenderUser(CacheService.Options.MyId);
                var startDate = 0;

                if (popup.IsScheduleSelected)
                {
                    var schedule = new ScheduleVoiceChatPopup(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel);

                    var again = await schedule.ShowQueuedAsync();
                    if (again != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    startDate = schedule.Value.ToTimestamp();
                }

                var response = await ProtoService.SendAsync(new CreateVoiceChat(chat.Id, string.Empty, startDate));
                if (response is GroupCallId groupCallId)
                {
                    await JoinAsyncInternal(chat, groupCallId.Id, participantId);
                }
            }
        }

        private async Task JoinAsyncInternal(Chat chat, int groupCallId, MessageSender alias)
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
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.VoipOngoingChatAlert, _chat.Title, chat.Title), Strings.Resources.VoipOngoingChatAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    await DisposeAsync(false);
                }
            }

            alias ??= chat.VoiceChat.DefaultParticipantId;

            if (alias == null)
            {
                alias = await PickAliasAsync(chat, false);
            }

            var response = await ProtoService.SendAsync(new GetGroupCall(groupCallId));
            if (response is GroupCall groupCall)
            {
                _chat = chat;
                _call = groupCall;

                _isScheduled = groupCall.ScheduledStartDate > 0;

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
                _manager.BroadcastPartRequested += OnBroadcastPartRequested;

                // This must be set before, as updates might come
                // between ShowAsync and Rejoin.
                _isJoining = true;

                await ShowAsync();

                if (groupCall.ScheduledStartDate > 0)
                {
                    ProtoService.Send(new SetVoiceChatDefaultParticipant(chat.Id, alias));
                }
                else
                {
                    Rejoin(groupCall, alias);
                }
            }
        }

        public async Task RejoinAsync()
        {
            var call = _call;
            var chat = _chat;

            if (call == null || chat == null)
            {
                return;
            }

            var alias = await PickAliasAsync(chat, true);
            if (alias != null)
            {
                Rejoin(call, alias);
            }
        }

        private async Task<MessageSender> PickAliasAsync(Chat chat, bool darkTheme)
        {
            var available = await CanChooseAliasAsync(chat.Id);
            if (available && _availableAliases != null)
            {
                var popup = new VoiceChatAliasesPopup(ProtoService, chat, false, _availableAliases.Senders.ToArray());
                popup.RequestedTheme = darkTheme ? ElementTheme.Dark : ElementTheme.Default;

                var confirm = await popup.ShowQueuedAsync();
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

            _manager?.SetConnectionMode(VoipGroupConnectionMode.None, false);
            _manager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                Participants ??= new GroupCallParticipantsCollection(ProtoService, Aggregator, groupCall);

                var response = await ProtoService.SendAsync(new JoinGroupCall(groupCall.Id, alias, ssrc, payload, _manager.IsMuted, _capturer != null, string.Empty));
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
                    _manager.SetConnectionMode(broadcast ? VoipGroupConnectionMode.Broadcast : VoipGroupConnectionMode.Rtc, true);
                    _manager.SetJoinResponsePayload(json.TextValue);

                    RejoinScreenSharing(groupCall);
                }
            });

        }

        #region Capturing

        public bool CanEnableVideo
        {
            get
            {
                var currentUser = _currentUser;
                if (currentUser != null)
                {
                    return currentUser.CanEnableVideo && !(currentUser.IsMutedForAllUsers && !currentUser.CanUnmuteSelf);
                }

                return _call?.CanStartVideo ?? false;
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

            ProtoService.Send(new ToggleGroupCallIsMyVideoEnabled(call.Id, _capturer != null));
        }

        #endregion

        #region Screencast

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
                ProtoService.SendAsync(new ToggleGroupCallScreenSharingIsPaused(call.Id, paused));
            }
        }

        private void OnFatalErrorOccurred(VoipScreenCapture sender, object args)
        {
            EndScreenSharing();
        }

        private void RejoinScreenSharing(GroupCall groupCall)
        {
            _screenManager?.SetConnectionMode(VoipGroupConnectionMode.None, false);
            _screenManager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                var response = await ProtoService.SendAsync(new StartGroupCallScreenSharing(groupCall.Id, payload));
                if (response is Text json)
                {
                    if (_screenManager == null)
                    {
                        return;
                    }

                    _screenSource = ssrc;
                    _screenManager.SetConnectionMode(VoipGroupConnectionMode.Rtc, true);
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
                ProtoService.Send(new EndGroupCallScreenSharing(call.Id));
            }
        }

        #endregion

        private async void OnBroadcastPartRequested(VoipGroupManager sender, BroadcastPartRequestedEventArgs args)
        {
            var call = _call;
            if (call == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetOption("unix_time")) as OptionValueInteger;
            if (response == null)
            {
                return;
            }

            var time = args.Time;
            if (time == 0)
            {
                time = response.Value * 1000;
            }

            var stamp = DateTime.Now.ToTimestamp();

            ProtoService.Send(new GetGroupCallStreamSegment(call.Id, time, args.Scale), result =>
            {
                stamp = DateTime.Now.ToTimestamp() - stamp;
                args.Deferral(time, response.Value + stamp, result as FilePart);
            });
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
                ProtoService.Send(new SetGroupCallParticipantIsSpeaking(call.Id, item.Key, validSpeakers.ContainsKey(item.Key)));
            }

            _speakingParticipants = validSpeakers;
        }

        public Task DiscardAsync()
        {
            return DisposeAsync(true);
        }

        private Task DisposeAsync(bool? discard = null)
        {
            if (_call != null && discard is true)
            {
                ProtoService.Send(new DiscardGroupCall(_call.Id));
            }
            else if (_call != null && discard is false)
            {
                ProtoService.Send(new LeaveGroupCall(_call.Id));
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

            return Task.Run(() =>
            {
                if (_manager != null)
                {
                    _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                    _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
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
            });
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

        public bool IsNoiseSuppressionEnabled
        {
            get => _manager.IsNoiseSuppressionEnabled;
            set => _manager.IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled = value;
        }

        public async void Handle(UpdateGroupCall update)
        {
            if (_call?.Id == update.GroupCall.Id)
            {
                if (update.GroupCall.IsJoined || update.GroupCall.NeedRejoin || _isJoining)
                {
                    _call = update.GroupCall;

                    var lifetime = _callLifetime;
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
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
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

                var lifetime = _callLifetime;
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
        }

        public Chat Chat => _chat;
        public GroupCall Call => _call;
        public GroupCallParticipant CurrentUser => _currentUser;
        public int Source => _source;

        public bool IsConnected => _isConnected;

        public async Task ShowAsync()
        {
            using (await _updateLock.WaitAsync())
            {
                if (_callLifetime == null)
                {
                    var parameters = new ViewServiceParams
                    {
                        Title = Strings.Resources.VoipGroupVoiceChat,
                        Width = 380,
                        Height = 580,
                        PersistentId = "VoiceChat",
                        Content = control => new GroupCallPage(ProtoService, CacheService, Aggregator, this)
                    };

                    _callLifetime = await _viewService.OpenAsync(parameters);
                    _callLifetime.Closed += ApplicationView_Released;
                    _callLifetime.Released += ApplicationView_Released;

                    //Aggregator.Publish(new UpdateCallDialog(call, true));
                }
                else
                {
                    await ApplicationViewSwitcher.SwitchAsync(_callLifetime.Id);
                }
            }
        }

        public async Task ConsolidateAsync()
        {
            using (await _updateLock.WaitAsync())
            {
                //Aggregator.Publish(new UpdateCallDialog(_call, true));

                var lifetime = _callLifetime;
                if (lifetime != null)
                {
                    _callLifetime = null;
                    await lifetime.Dispatcher.DispatchAsync(() => ApplicationView.GetForCurrentView().ConsolidateAsync());
                }
            }
        }

        private async void ApplicationView_Released(object sender, EventArgs e)
        {
            using (await _updateLock.WaitAsync())
            {
                _callLifetime = null;
                //Aggregator.Publish(new UpdateCallDialog(_call, false));
            }
        }
    }
}
