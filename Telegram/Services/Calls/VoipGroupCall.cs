//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.Views.Calls;
using Telegram.Views.Calls.Popups;
using Windows.ApplicationModel.Calls;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services.Calls
{
    public enum VoipGroupCallStreamState
    {
        Unknown,
        NotAvailable,
        Available
    }

    public partial class VoipGroupCall : VoipCallBase
    {
        private readonly IViewService _viewService;

        private readonly Chat _chat;
        private readonly string _inviteHash;
        private readonly bool _isLiveStory;

        private InputGroupCall _inputGroupCall;
        private IList<long> _inviteUserIds;

        private TaskCompletionSource<InputGroupCall> _inputGroupCallTask;

        private MessageSender _alias;
        private MessageSenders _availableAliases;
        private TaskCompletionSource<MessageSenders> _availableAliasesTask;

        private GroupCallParticipant _currentUser;

        private readonly object _managerLock = new();

        private VoipGroupManager _manager;
        private VoipVideoCapture _capturer;
        private int _source;

        private VoipGroupManager _screenManager;
        private VoipScreenCapture _screenCapturer;
        //private EventDebouncer<bool> _screenDebouncer;
        private int _screenSource;

        private readonly MediaDeviceTracker _devices = new();

        private readonly List<PaidReactor> _topDonors = new();
        private readonly object _topDonorsLock = new();

        private long _totalStarCount;

        private readonly List<GroupCallMessage> _messages = new();
        private readonly List<GroupCallMessage> _pinnedMessages = new();
        private readonly object _messagesLock = new();

        private Timer _pinnedMessagesTimer;

        private GroupCallParticipant _streamer;
        private readonly object _streamerLock = new();

        private VoipCallCoordinator _coordinator;
        private VoipPhoneCall _systemCall;

        private bool _isScheduled;
        private bool _isConnected;
        private bool _isClosed;

        private VoipGroupCallStreamState _streamState;
        private readonly object _streamStateLock = new();

        public VoipGroupCall(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, XamlRoot xamlRoot, Chat chat, GroupCall groupCall, MessageSender alias, string inviteHash, bool isLiveStory)
            : base(clientService, settingsService, aggregator)
        {
            Duration = groupCall.Duration;
            IsVideoRecorded = groupCall.IsVideoRecorded;
            RecordDuration = groupCall.RecordDuration;
            CanToggleMuteNewParticipants = groupCall.CanToggleMuteNewParticipants;
            CanSendMessages = groupCall.CanSendMessages;
            AreMessagesAllowed = groupCall.AreMessagesAllowed;
            CanToggleAreMessagesAllowed = groupCall.CanToggleAreMessagesAllowed;
            CanDeleteMessages = groupCall.CanDeleteMessages;
            MuteNewParticipants = groupCall.MuteNewParticipants;
            CanEnableVideo2 = groupCall.CanEnableVideo;
            IsMyVideoPaused = groupCall.IsMyVideoPaused;
            IsMyVideoEnabled = groupCall.IsMyVideoEnabled;
            RecentSpeakers = groupCall.RecentSpeakers;
            LoadedAllParticipants = groupCall.LoadedAllParticipants;
            MessageSenderId = groupCall.MessageSenderId;
            HasHiddenListeners = groupCall.HasHiddenListeners;
            ParticipantCount = groupCall.ParticipantCount;
            CanBeManaged = groupCall.CanBeManaged;
            NeedRejoin = groupCall.NeedRejoin;
            IsJoined = groupCall.IsJoined;
            IsRtmpStream = groupCall.IsRtmpStream;
            IsLiveStory = groupCall.IsLiveStory;
            IsActive = groupCall.IsActive;
            EnabledStartNotification = groupCall.EnabledStartNotification;
            ScheduledStartDate = groupCall.ScheduledStartDate;
            Title = groupCall.Title;
            IsOwned = groupCall.IsOwned;
            IsVideoChat = groupCall.IsVideoChat;
            InviteLink = groupCall.InviteLink;
            PaidMessageStarCount = groupCall.PaidMessageStarCount;
            UniqueId = groupCall.UniqueId;
            Id = groupCall.Id;

            _chat = chat;
            _inviteHash = inviteHash ?? string.Empty;
            _isLiveStory = isLiveStory;

            _isScheduled = groupCall.ScheduledStartDate > 0;

            _devices.Changed += OnDeviceChanged;

            var descriptor = new VoipGroupDescriptor
            {
                IsConference = false,
                //AudioInputId = _inputWatcher.GetAndUpdateAsync().Result,
                //AudioOutputId = _outputWatcher.GetAndUpdateAsync().Result,
                IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled
            };

            _manager = new VoipGroupManager(descriptor);
            _manager.NetworkStateUpdated += OnNetworkStateUpdated;
            _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _manager.BroadcastTimeRequested += OnBroadcastTimeRequested;
            _manager.AudioBroadcastPartRequested += OnAudioBroadcastPartRequested;
            _manager.VideoBroadcastPartRequested += OnVideoBroadcastPartRequested;
            _manager.MediaChannelDescriptionsRequested += OnMediaChannelDescriptionsRequested;

            if (!_isLiveStory)
            {
                InitializeSystemCallAsync(groupCall.Id, groupCall.Title).Wait();
                CreateWindow(false);

                _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);
            }
            else
            {
                Aggregator.Subscribe<UpdateGroupCall>(this, Handle, EventType.GroupCall, Id)
                    .Subscribe<UpdateGroupCallParticipant>(Handle)
                    .Subscribe<UpdateGroupCallVerificationState>(Handle)
                    .Subscribe<UpdateGroupCallMessageSendFailed>(Handle)
                    .Subscribe<UpdateGroupCallMessagesDeleted>(Handle)
                    .Subscribe<UpdateNewGroupCallMessage>(Handle)
                    .Subscribe<UpdateNewGroupCallPaidReaction>(Handle)
                    .Subscribe<UpdateLiveStoryTopDonors>(Handle);
            }

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

        public VoipGroupCall(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, XamlRoot xamlRoot, InputGroupCall inputGroupCall)
            : base(clientService, settingsService, aggregator)
        {
            //Duration = groupCall.Duration;
            //IsVideoRecorded = groupCall.IsVideoRecorded;
            //RecordDuration = groupCall.RecordDuration;
            //CanToggleMuteNewParticipants = groupCall.CanToggleMuteNewParticipants;
            //MuteNewParticipants = groupCall.MuteNewParticipants;
            //CanEnableVideo2 = groupCall.CanEnableVideo;
            //IsMyVideoPaused = groupCall.IsMyVideoPaused;
            //IsMyVideoEnabled = groupCall.IsMyVideoEnabled;
            //RecentSpeakers = groupCall.RecentSpeakers;
            //LoadedAllParticipants = groupCall.LoadedAllParticipants;
            //HasHiddenListeners = groupCall.HasHiddenListeners;
            //ParticipantCount = groupCall.ParticipantCount;
            //CanBeManaged = groupCall.CanBeManaged;
            //NeedRejoin = groupCall.NeedRejoin;
            //IsJoined = groupCall.IsJoined;
            //IsRtmpStream = groupCall.IsRtmpStream;
            //IsActive = groupCall.IsActive;
            //EnabledStartNotification = groupCall.EnabledStartNotification;
            //ScheduledStartDate = groupCall.ScheduledStartDate;
            //Title = groupCall.Title;
            //IsOwned = groupCall.IsOwned;
            //IsVideoChat = groupCall.IsVideoChat;
            //InviteLink = groupCall.InviteLink;
            //Id = groupCall.Id;

            _inputGroupCall = inputGroupCall;

            _inputGroupCallTask = new TaskCompletionSource<InputGroupCall>();
            _inputGroupCallTask.SetResult(inputGroupCall);

            _isScheduled = false;

            _devices.Changed += OnDeviceChanged;

            var descriptor = new VoipGroupDescriptor
            {
                IsConference = true,
                //AudioInputId = _inputWatcher.GetAndUpdateAsync().Result,
                //AudioOutputId = _outputWatcher.GetAndUpdateAsync().Result,
                IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled
            };

            _manager = new VoipGroupManager(descriptor);
            _manager.NetworkStateUpdated += OnNetworkStateUpdated;
            _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _manager.BroadcastTimeRequested += OnBroadcastTimeRequested;
            _manager.AudioBroadcastPartRequested += OnAudioBroadcastPartRequested;
            _manager.VideoBroadcastPartRequested += OnVideoBroadcastPartRequested;
            _manager.MediaChannelDescriptionsRequested += OnMediaChannelDescriptionsRequested;
            _manager.SetEncryptDecrypt(EncryptData, DecryptData);

            _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);

            InitializeSystemCallAsync(0, "groupCall.Title").Wait();
            CreateWindow(false);

            Rejoin(clientService.MyId);
        }

        public VoipGroupCall(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, XamlRoot xamlRoot, IList<long> userIds)
            : base(clientService, settingsService, aggregator)
        {
            //Duration = groupCall.Duration;
            //IsVideoRecorded = groupCall.IsVideoRecorded;
            //RecordDuration = groupCall.RecordDuration;
            //CanToggleMuteNewParticipants = groupCall.CanToggleMuteNewParticipants;
            //MuteNewParticipants = groupCall.MuteNewParticipants;
            //CanEnableVideo2 = groupCall.CanEnableVideo;
            //IsMyVideoPaused = groupCall.IsMyVideoPaused;
            //IsMyVideoEnabled = groupCall.IsMyVideoEnabled;
            //RecentSpeakers = groupCall.RecentSpeakers;
            //LoadedAllParticipants = groupCall.LoadedAllParticipants;
            //HasHiddenListeners = groupCall.HasHiddenListeners;
            //ParticipantCount = groupCall.ParticipantCount;
            //CanBeManaged = groupCall.CanBeManaged;
            //NeedRejoin = groupCall.NeedRejoin;
            //IsJoined = groupCall.IsJoined;
            //IsRtmpStream = groupCall.IsRtmpStream;
            //IsActive = groupCall.IsActive;
            //EnabledStartNotification = groupCall.EnabledStartNotification;
            //ScheduledStartDate = groupCall.ScheduledStartDate;
            //Title = groupCall.Title;
            //IsOwned = groupCall.IsOwned;
            //IsVideoChat = groupCall.IsVideoChat;
            //InviteLink = groupCall.InviteLink;
            //Id = groupCall.Id;

            _inviteUserIds = userIds;
            _inputGroupCallTask = new TaskCompletionSource<InputGroupCall>();

            _isScheduled = false;

            _devices.Changed += OnDeviceChanged;

            var descriptor = new VoipGroupDescriptor
            {
                IsConference = true,
                //AudioInputId = _inputWatcher.GetAndUpdateAsync().Result,
                //AudioOutputId = _outputWatcher.GetAndUpdateAsync().Result,
                IsNoiseSuppressionEnabled = Settings.VoIP.IsNoiseSuppressionEnabled
            };

            _manager = new VoipGroupManager(descriptor);
            _manager.NetworkStateUpdated += OnNetworkStateUpdated;
            _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _manager.BroadcastTimeRequested += OnBroadcastTimeRequested;
            _manager.AudioBroadcastPartRequested += OnAudioBroadcastPartRequested;
            _manager.VideoBroadcastPartRequested += OnVideoBroadcastPartRequested;
            _manager.MediaChannelDescriptionsRequested += OnMediaChannelDescriptionsRequested;
            _manager.SetEncryptDecrypt(EncryptData, DecryptData);

            _coordinator?.TryNotifyMutedChanged(_manager.IsMuted);

            InitializeSystemCallAsync(0, "groupCall.Title").Wait();
            CreateWindow(false);

            Rejoin(clientService.MyId);
        }

        public VoipGroupCallVerificationStateChangedEventArgs VerificationState { get; private set; }

        public IReadOnlyList<GroupCallMessage> Messages
        {
            get
            {
                lock (_messagesLock)
                {
                    return [.. _messages];
                }
            }
        }

        public IReadOnlyList<GroupCallMessage> PinnedMessages
        {
            get
            {
                lock (_messagesLock)
                {
                    return [.. _pinnedMessages];
                }
            }
        }

        public VoipGroupCallStreamState StreamState
        {
            get
            {
                lock (_streamStateLock)
                {
                    return _streamState;
                }
            }
        }

        public GroupCallParticipant Streamer
        {
            get
            {
                lock (_streamerLock)
                {
                    return _streamer;
                }
            }
        }

        private IList<byte> EncryptData(VoipDataChannel dataChannel, IList<byte> data, int unencryptedPrefixSize)
        {
            Data response = null;
            // TODO: optimize IList<byte> => byte[]
            ClientService.Send(new EncryptGroupCallData(Id, null, data.ToArray(), unencryptedPrefixSize), result =>
            {
                response = result as Data;
                _encryptMutex.Release();
            });

            _encryptMutex.WaitOne();
            return response?.DataValue ?? Array.Empty<byte>();
        }

        private IList<byte> DecryptData(long userId, IList<byte> data)
        {
            Data response = null;
            // TODO: optimize IList<byte> => byte[]
            ClientService.Send(new DecryptGroupCallData(Id, new MessageSenderUser(userId), new GroupCallDataChannelMain(), data.ToArray()), result =>
            {
                response = result as Data;
                _decryptMutex.Release();
            });

            _decryptMutex.WaitOne();
            return response?.DataValue ?? Array.Empty<byte>();
        }

        private readonly Semaphore _encryptMutex = new(0, 1);
        private readonly Semaphore _decryptMutex = new(0, 1);

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallNetworkStateChangedEventArgs> NetworkStateChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallJoinedStateChangedEventArgs> JoinedStateChanged;

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallVerificationStateChangedEventArgs> VerificationStateChanged;

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallMessagesChangedEventArgs> MessagesChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallMessagesChangedEventArgs> PinnedMessagesChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallReactionsChangedEventArgs> ReactionsChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallTopDonorsChangedEventArgs> TopDonorsChanged;
        public event TypedEventHandler<VoipGroupCall, VoipGroupCallTotalStarCountChangedEventArgs> TotalStarCountChanged;

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallStreamStateChangedEventArgs> StreamStateChanged;

        public event TypedEventHandler<VoipGroupCall, VoipGroupCallStreamerChangedEventArgs> StreamerChanged;

        private GroupCallParticipantsCollection _participants;
        public GroupCallParticipantsCollection Participants
        {
            get => _participants;
            private set => Set(ref _participants, value);
        }

        public async Task<bool> CanChooseAliasAsync()
        {
            if (Chat == null)
            {
                return false;
            }

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

        private async Task InitializeSystemCallAsync(int callId, string callTitle)
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
                    _systemCall = _coordinator.RequestNewOutgoingCall($"{callId}", callTitle, Strings.AppName, VoipPhoneCallMedia.Audio | VoipPhoneCallMedia.Video);
                    _systemCall.TryNotifyCallActive();
                    _systemCall.EndRequested += OnEndRequested;
                }
                else
                {
                    Logger.Error(status);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                _coordinator?.MuteStateChanged += OnMuteStateChanged;
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

                if (_inputGroupCallTask != null)
                {
                    await _inputGroupCallTask.Task;
                }

                var joinParameters = new GroupCallJoinParameters(ssrc, payload, _manager.IsMuted, _capturer != null);
                Function request = _inputGroupCall != null
                    ? new JoinGroupCall(_inputGroupCall, joinParameters)
                    : _inviteUserIds != null
                    ? new CreateGroupCall(joinParameters)
                    : _isLiveStory
                    ? new JoinLiveStory(Id, joinParameters)
                    : new JoinVideoChat(Id, alias, joinParameters, _inviteHash);

                var response = await ClientService.SendAsync(request);
                if (response is GroupCallInfo info)
                {
                    Id = info.GroupCallId;
                    response = new Text(info.JoinPayload);

                    if (ClientService.TryGetGroupCall(info.GroupCallId, out GroupCall groupCall))
                    {
                        _inputGroupCall ??= new InputGroupCallLink(groupCall.InviteLink);
                        _inputGroupCallTask.TrySetResult(_inputGroupCall);
                        Update(groupCall, out _);
                    }

                    if (_inviteUserIds != null)
                    {
                        foreach (var userId in _inviteUserIds)
                        {
                            ClientService.Send(new InviteGroupCallParticipant(info.GroupCallId, userId, false));
                        }

                        _inviteUserIds = null;
                    }
                }

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

                if (IsLiveStory && !IsRtmpStream)
                {
                    InitializeStreamer();
                }
            });
        }

        private async void InitializeStreamer()
        {
            GroupCallParticipant streamerChanged = null;

            var response = await ClientService.SendAsync(new GetLiveStoryStreamer(Id));
            if (response is GroupCallParticipant participant)
            {
                lock (_streamerLock)
                {
                    _streamer = participant;
                    streamerChanged = participant;
                }
            }

            if (streamerChanged != null)
            {
                StreamerChanged?.Invoke(this, new VoipGroupCallStreamerChangedEventArgs(streamerChanged));
            }
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

        public async void StartScreenSharing(XamlRoot xamlRoot)
        {
            if (_manager == null || _screenManager != null || !VoipScreenCapture.IsSupported())
            {
                return;
            }

            var item = await CaptureSessionService.ChooseAsync(xamlRoot, true);
            if (item == null || _manager == null || _screenManager != null)
            {
                return;
            }

            _screenCapturer = new VoipScreenCapture(item.CaptureItem);
            _screenCapturer.FatalErrorOccurred += OnFatalErrorOccurred;

            // TODO: currently Paused is triggered when frames are dropped as well.
            // This can happen because of high resource usage or even optimization (no changes on the screen => no frames available)
            //_screenDebouncer = new EventDebouncer<bool>(500,
            //    handler => _screenCapturer.Paused += new TypedEventHandler<VoipScreenCapture, bool>(handler),
            //    handler => _screenCapturer.Paused -= new TypedEventHandler<VoipScreenCapture, bool>(handler), true);
            //_screenDebouncer.Invoked += OnPaused;

            var descriptor = new VoipGroupDescriptor
            {
                IsConference = !IsVideoChat,
                VideoContentType = VoipVideoContentType.Screencast,
                VideoCapture = _screenCapturer,
                AudioProcessId = item.ProcessId
            };

            _screenManager = new VoipGroupManager(descriptor);
            _screenManager.SetEncryptDecrypt(EncryptData, DecryptData);

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
                _screenManager.SetEncryptDecrypt(null, null);
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
                var streamStateChanged = VoipGroupCallStreamState.Unknown;

                var response = await ClientService.SendAsync(new GetGroupCallStreams(Id));
                if (response is GroupCallStreams streams && streams.Streams.Count > 0)
                {
                    args.Deferral(streams.Streams[0].TimeOffset);

                    lock (_streamStateLock)
                    {
                        if (_streamState != VoipGroupCallStreamState.Available)
                        {
                            _streamState = VoipGroupCallStreamState.Available;
                            streamStateChanged = VoipGroupCallStreamState.Available;
                        }
                    }
                }
                else
                {
                    args.Deferral(0);

                    lock (_streamStateLock)
                    {
                        if (_streamState != VoipGroupCallStreamState.NotAvailable)
                        {
                            _streamState = VoipGroupCallStreamState.NotAvailable;
                            streamStateChanged = VoipGroupCallStreamState.NotAvailable;
                        }
                    }
                }

                if (streamStateChanged != VoipGroupCallStreamState.Unknown)
                {
                    StreamStateChanged?.Invoke(this, new VoipGroupCallStreamStateChangedEventArgs(streamStateChanged));
                }
            }
            else
            {
                args.Deferral(ClientService.UnixTimeMilliseconds);
            }
        }

        private async void OnAudioBroadcastPartRequested(VoipGroupManager sender, AudioBroadcastPartRequestedEventArgs args)
        {
            var response = await ClientService.SendAsync(new GetGroupCallStreamSegment(Id, args.Time, args.Scale, 0, null));
            if (response is Data data)
            {
                args.Deferral(args.Time, ClientService.UnixTimeMilliseconds, data.DataValue);
            }
            else
            {
                args.Deferral(args.Time, ClientService.UnixTimeMilliseconds, null);
            }
        }

        private async void OnVideoBroadcastPartRequested(VoipGroupManager sender, VideoBroadcastPartRequestedEventArgs args)
        {
            GroupCallVideoQuality videoQuality = args.VideoQuality switch
            {
                VoipVideoChannelQuality.Thumbnail => new GroupCallVideoQualityThumbnail(),
                VoipVideoChannelQuality.Medium => new GroupCallVideoQualityMedium(),
                VoipVideoChannelQuality.Full => new GroupCallVideoQualityFull(),
                _ => null
            };

            var response = await ClientService.SendAsync(new GetGroupCallStreamSegment(Id, args.Time, args.Scale, args.ChannelId, videoQuality));
            if (response is Data data)
            {
                args.Deferral(args.Time, ClientService.UnixTimeMilliseconds, data.DataValue);
            }
            else
            {
                args.Deferral(args.Time, ClientService.UnixTimeMilliseconds, null);
            }
        }

        private async void OnMediaChannelDescriptionsRequested(VoipGroupManager sender, MediaChannelDescriptionsRequestedEventArgs args)
        {
            HashSet<int> unknownSources = null;

            var participants = Participants;
            if (participants == null)
            {
                args.Deferral(Array.Empty<VoipMediaChannelDescription>());
            }

            var knownSources = participants.ToDictionary();
            var result = new List<VoipMediaChannelDescription>(args.AudioSourceIds.Count);

            foreach (var ssrc in args.AudioSourceIds)
            {
                if (knownSources.TryGetValue((int)ssrc, out GroupCallParticipant participant))
                {
                    result.Add(new VoipMediaChannelDescription
                    {
                        AudioSource = participant.AudioSourceId,
                        UserId = participant.ParticipantId.ToId()
                    });
                }
                else
                {
                    unknownSources ??= new HashSet<int>();
                    unknownSources.Add((int)ssrc);
                }
            }

            if (unknownSources?.Count > 0 && Id != 0)
            {
                // Currently tgcalls always passes a single ssrc requestMediaChannelDescriptions,
                // so it's fine to call SetGroupCallParticipantIsSpeaking that will load the participant if missing
                foreach (var ssrc in unknownSources)
                {
                    await ClientService.SendAsync(new SetGroupCallParticipantIsSpeaking(Id, ssrc, true));
                }

                knownSources = participants.ToDictionary();

                foreach (var ssrc in args.AudioSourceIds)
                {
                    if (knownSources.TryGetValue((int)ssrc, out GroupCallParticipant participant))
                    {
                        result.Add(new VoipMediaChannelDescription
                        {
                            AudioSource = participant.AudioSourceId,
                            UserId = participant.ParticipantId.ToId()
                        });
                    }
                }
            }

            args.Deferral(result);
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

            Logger.Info(string.Format("Connected: {0}", args.IsConnected));

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
                var prevSpeaking = _speakingParticipants != null && _speakingParticipants.ContainsKey(item.AudioSource);
                var nextSpeaking = validSpeakers != null && validSpeakers.ContainsKey(item.AudioSource);

                if (nextSpeaking != prevSpeaking)
                {
                    ClientService.Send(new SetGroupCallParticipantIsSpeaking(Id, item.AudioSource, nextSpeaking));
                }
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
                ThreadPool.QueueUserWorkItem(state => Aggregator.Publish(new UpdateGroupCall(new GroupCall(Id, UniqueId, Title, InviteLink, PaidMessageStarCount, ScheduledStartDate, EnabledStartNotification, IsActive, IsVideoChat, IsLiveStory, IsRtmpStream, false, false, IsOwned, CanBeManaged, ParticipantCount, HasHiddenListeners, LoadedAllParticipants, MessageSenderId, RecentSpeakers, IsMyVideoEnabled, IsMyVideoPaused, CanEnableVideo2, MuteNewParticipants, CanToggleMuteNewParticipants, CanSendMessages, AreMessagesAllowed, CanToggleAreMessagesAllowed, CanDeleteMessages, RecordDuration, IsVideoRecorded, Duration))));
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
            //_chat = null;

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
                _manager.AudioBroadcastPartRequested -= OnAudioBroadcastPartRequested;
                _manager.VideoBroadcastPartRequested -= OnVideoBroadcastPartRequested;
                _manager.MediaChannelDescriptionsRequested -= OnMediaChannelDescriptionsRequested;

                _manager.SetEncryptDecrypt(null, null);
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
                _manager?.IsNoiseSuppressionEnabled = value;

                Settings.VoIP.IsNoiseSuppressionEnabled = value;
            }
        }

        private double _volumeLevel = 1;
        public double VolumeLevel
        {
            get => _volumeLevel;
            set => _manager?.SetVolume(1, _volumeLevel = value);
        }

        public bool IsClosed => _isClosed;

        public void Handle(UpdateGroupCall update)
        {
            Update(update.GroupCall, out bool closed);

            if (closed)
            {
                Aggregator.Unsubscribe(this);
            }
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            UpdateParticipant(update.Participant);
        }

        public void Handle(UpdateGroupCallVerificationState update)
        {
            UpdateVerificationState(update.Generation, update.Emojis);
        }

        public void Handle(UpdateGroupCallMessageSendFailed update)
        {
            Logger.Info(update);
        }

        public void Handle(UpdateGroupCallMessagesDeleted update)
        {
            UpdateMessagesDeleted(update.MessageIds);
        }

        public void Handle(UpdateNewGroupCallMessage update)
        {
            UpdateNewMessage(update.Message);
        }

        public void Handle(UpdateNewGroupCallPaidReaction update)
        {
            ReactionsChanged?.Invoke(this, new VoipGroupCallReactionsChangedEventArgs(update.SenderId, update.StarCount));
        }

        public void Handle(UpdateLiveStoryTopDonors update)
        {
            var topDonorsChanged = default(List<PaidReactor>);
            var totalStarCountChanged = -1L;

            lock (_topDonorsLock)
            {
                var prevSorted = _topDonors.Where(x => x.IsTop).OrderByDescending(x => x.StarCount).ToList();
                var nextSorted = update.Donors.TopDonors.Where(x => x.IsTop).OrderByDescending(x => x.StarCount).ToList();

                if (prevSorted.Count == nextSorted.Count)
                {
                    for (int i = 0; i < prevSorted.Count; i++)
                    {
                        if (!prevSorted[i].SenderId.AreTheSame(nextSorted[i].SenderId))
                        {
                            topDonorsChanged = nextSorted;
                            break;
                        }
                    }
                }
                else
                {
                    topDonorsChanged = nextSorted;
                }

                if (topDonorsChanged != null)
                {
                    _topDonors.Clear();
                    _topDonors.AddRange(nextSorted);
                }

                if (_totalStarCount != update.Donors.TotalStarCount)
                {
                    _totalStarCount = update.Donors.TotalStarCount;
                    totalStarCountChanged = update.Donors.TotalStarCount;
                }
            }

            if (topDonorsChanged != null)
            {
                TopDonorsChanged?.Invoke(this, new VoipGroupCallTopDonorsChangedEventArgs(topDonorsChanged));
            }

            if (totalStarCountChanged != -1)
            {
                TotalStarCountChanged?.Invoke(this, new VoipGroupCallTotalStarCountChangedEventArgs(totalStarCountChanged));
            }
        }

        public void Update(GroupCall call, out bool closed)
        {
            closed = false;

            var isJoined = IsJoined;
            var needRejoin = NeedRejoin;

            Duration = call.Duration;
            IsVideoRecorded = call.IsVideoRecorded;
            RecordDuration = call.RecordDuration;
            CanToggleMuteNewParticipants = call.CanToggleMuteNewParticipants;
            CanSendMessages = call.CanSendMessages;
            AreMessagesAllowed = call.AreMessagesAllowed;
            CanToggleAreMessagesAllowed = call.AreMessagesAllowed;
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
            MessageSenderId = call.MessageSenderId;
            PaidMessageStarCount = call.PaidMessageStarCount;
            Title = call.Title;
            UniqueId = call.UniqueId;
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

        public void UpdateParticipant(GroupCallParticipant participant)
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
                manager.SetVolume(participant.ScreenSharingAudioSourceId, 0);
            }
            else
            {
                manager.SetVolume(participant.AudioSourceId, participant.VolumeLevel / 10000d);
                manager.SetVolume(participant.ScreenSharingAudioSourceId, participant.VolumeLevel / 10000d);
            }
        }

        public void UpdateVerificationState(int generation, IList<string> emojis)
        {
            VerificationState = new VoipGroupCallVerificationStateChangedEventArgs(generation, emojis);
            VerificationStateChanged?.Invoke(this, new VoipGroupCallVerificationStateChangedEventArgs(generation, emojis));
        }

        public void UpdateMessagesDeleted(IList<int> messageIds)
        {
            var hash = messageIds.ToHashSet();

            var deleted = default(List<GroupCallMessage>);
            var added = default(List<GroupCallMessage>);
            var removed = default(List<GroupCallMessage>);

            lock (_messagesLock)
            {
                for (int i = 0; i < _messages.Count; i++)
                {
                    var message = _messages[i];
                    if (hash.Contains(message.MessageId))
                    {
                        deleted ??= [];
                        deleted.Add(message);

                        _messages.Remove(message);
                        UpdatePinnedMessagesLocked(message, true, out var addedTemp, out var removedTemp);

                        if (removedTemp != null)
                        {
                            removed ??= [];
                            removed.Add(removedTemp);
                        }

                        if (addedTemp != null)
                        {
                            added ??= [];
                            added.Add(addedTemp);
                        }

                        i--;
                    }
                }
            }

            if (deleted != null)
            {
                foreach (var message in deleted)
                {
                    MessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(message, true));
                }
            }

            if (removed != null)
            {
                foreach (var message in removed)
                {
                    PinnedMessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(message, true));
                }
            }

            if (added != null)
            {
                foreach (var message in added)
                {
                    PinnedMessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(message, false));
                }
            }
        }

        public void UpdateNewMessage(GroupCallMessage message)
        {
            GroupCallMessage added;
            GroupCallMessage removed;

            lock (_messagesLock)
            {
                UpdatePinnedMessagesLocked(message, false, out added, out removed);

                _messages.Add(message);
            }

            MessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(message, false));

            if (removed != null)
            {
                PinnedMessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(removed, true));
            }

            if (added != null)
            {
                PinnedMessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(added, false));
            }
        }

        private void UpdatePinnedMessagesLocked(GroupCallMessage message, bool expired, out GroupCallMessage added, out GroupCallMessage removed)
        {
            added = null;
            removed = null;

            var now = ClientService.UnixTime;

            if (expired)
            {
                _pinnedMessages.Remove(message);
                removed = message;

                var prev = _messages.FirstOrDefault(x => x.SenderId.AreTheSame(message.SenderId));
                if (prev != null)
                {
                    var expiration = GetExpiration(prev.Date, prev.PaidMessageStarCount);
                    if (expiration > now)
                    {
                        _pinnedMessages.Add(prev);
                        added = prev;
                    }
                }
            }
            else
            {
                var prev = _pinnedMessages.FirstOrDefault(x => x.SenderId.AreTheSame(message.SenderId));
                var prevExpiration = prev != null ? GetExpiration(prev.Date, prev.PaidMessageStarCount) : now;

                var expiration = GetExpiration(message.Date, message.PaidMessageStarCount);
                if (expiration > prevExpiration && expiration > now)
                {
                    if (prev != null)
                    {
                        _pinnedMessages.Remove(prev);
                        removed = prev;
                    }

                    _pinnedMessages.Add(message);
                    added = message;
                }
            }

            if (_pinnedMessages.Empty())
            {
                _pinnedMessagesTimer?.Dispose();
                _pinnedMessagesTimer = null;
            }
            else if (_pinnedMessagesTimer == null)
            {
                _pinnedMessagesTimer = new Timer(OnPinnedMessagesTick);
                _pinnedMessagesTimer.Change(1000, 1000);
            }
        }

        private void OnPinnedMessagesTick(object state)
        {
            var now = ClientService.UnixTime;

            var deleted = default(List<GroupCallMessage>);

            lock (_messagesLock)
            {
                for (int i = 0; i < _pinnedMessages.Count; i++)
                {
                    var message = _pinnedMessages[i];
                    var expiration = GetExpiration(message.Date, message.PaidMessageStarCount);

                    if (expiration < now)
                    {
                        deleted ??= [];
                        deleted.Add(message);

                        _pinnedMessages.Remove(message);
                        i--;
                    }
                }

                if (_pinnedMessages.Empty())
                {
                    _pinnedMessagesTimer?.Dispose();
                    _pinnedMessagesTimer = null;
                }
            }

            if (deleted != null)
            {
                foreach (var message in deleted)
                {
                    PinnedMessagesChanged?.Invoke(this, new VoipGroupCallMessagesChangedEventArgs(message, true));
                }
            }
        }

        private long GetExpiration(int date, long starCount)
        {
            if (ClientService.TryGetGroupCallMessageLevel(starCount, out GroupCallMessageLevel level))
            {
                if (level.PinDuration > 0)
                {
                    return date + level.PinDuration;
                }
            }

            return 0;
        }

        public void SendMessage(FormattedText text, long paidMessageStarCount = 0)
        {
            ClientService.Send(new SendGroupCallMessage(Id, text, paidMessageStarCount));
        }

        public string GetTitle()
        {
            if (_chat != null)
            {
                if (string.IsNullOrEmpty(Title))
                {
                    return _chat.Title;
                }

                return Title;
            }

            return Strings.ConferenceChat;
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
        /// True, if users can send messages to the group call.
        /// </summary>
        public bool CanSendMessages { get; private set; }

        /// <summary>
        /// True, if users can send messages to the group call.
        /// </summary>
        public bool AreMessagesAllowed { get; private set; }

        /// <summary>
        /// True, if the current user can enable or disable sending messages in the group
        /// call.
        /// </summary>
        public bool CanToggleAreMessagesAllowed { get; private set; }

        public bool CanDeleteMessages { get; private set; }

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

        public MessageSender MessageSenderId { get; private set; }

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
        /// True, if the user is the owner of the call and can end the call, change volume
        /// level of other users, or ban users there; for group calls that aren't bound to
        /// a chat.
        /// </summary>
        public bool IsOwned { get; private set; }

        /// <summary>
        /// True, if the chat is bound to a chat.
        /// </summary>
        public bool IsVideoChat { get; private set; }

        public bool IsLiveStory { get; private set; }

        /// <summary>
        /// Invite link for the group call; for group calls that aren't bound to a chat.
        /// </summary>
        public string InviteLink { get; private set; }

        public long PaidMessageStarCount { get; private set; }

        /// <summary>
        /// Group call identifier.
        /// </summary>
        public int Id { get; private set; }

        public long UniqueId { get; private set; }

        public InputGroupCall InputId => _inputGroupCall;

        public int Source => _source;

        public bool IsConnected => _isConnected;

        private void CreateWindow(bool upgrade)
        {
            if (upgrade)
            {
                WindowContext.ForEach(window =>
                {
                    if (window.Content is VoipPage)
                    {
                        window.Content = CreatePresentation(null);
                    }
                });
            }
            else
            {
                var service = ClientService.Session.Resolve<IViewService>();
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
