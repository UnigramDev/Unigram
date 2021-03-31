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
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IGroupCallService : INotifyPropertyChanged
    {
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }

        VoipGroupManager Manager { get; }

        GroupCallParticipantsCollection Participants { get; }

        Chat Chat { get; }
        GroupCall Call { get; }
        int Source { get; }
        bool IsConnected { get; }

        void Show();

        Task<bool> CanChooseAliasAsync(long chatId);

        Task JoinAsync(long chatId, bool pickAlias = false);
        Task RejoinAsync();
        void Leave();

        Task CreateAsync(long chatId);
        void Discard();
    }

    public class GroupCallService : TLViewModelBase, IGroupCallService, IHandle<UpdateGroupCall>, IHandle<UpdateGroupCallParticipant>
    {
        private readonly IViewService _viewService;

        private readonly MediaDeviceWatcher _inputWatcher;
        private readonly MediaDeviceWatcher _outputWatcher;

        private readonly DisposableMutex _updateLock = new DisposableMutex();

        private Chat _chat;

        private MessageSender _alias;
        private MessageSenders _availableAliases;
        private TaskCompletionSource<MessageSenders> _availableAliasesTask;

        private GroupCall _call;
        private VoipGroupManager _manager;
        private int _source;

        private bool _isConnected;

        private GroupCallPage _callPage;
        private ViewLifetimeControl _callLifetime;

        public GroupCallService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IViewService viewService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _viewService = viewService;

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
                var yolo = await tsc.Task;
                return yolo.TotalCount > 1;
            }

            tsc = _availableAliasesTask = new TaskCompletionSource<MessageSenders>();

            var result = await CanChooseAliasAsyncInternal(chatId);
            tsc.TrySetResult(result);

            return result.TotalCount > 1;
        }

        private async Task<MessageSenders> CanChooseAliasAsyncInternal(long chatId)
        {
            var response = await ProtoService.SendAsync(new GetAvailableVoiceChatAliases(chatId));
            if (response is MessageSenders senders)
            {
                _availableAliases = senders;
                _availableAliasesTask.TrySetResult(senders);
                return senders;
            }

            return null;
        }


        public async Task JoinAsync(long chatId, bool pickAlias)
        {
            var permissions = await MediaDeviceWatcher.CheckAccessAsync(false);
            if (permissions == false)
            {
                return;
            }

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

            await JoinAsyncInternal(chat, chat.VoiceChat.GroupCallId, pickAlias);
        }

        public async void Leave()
        {
            await DisposeAsync(false);
        }

        public async Task CreateAsync(long chatId)
        {
            var permissions = await MediaDeviceWatcher.CheckAccessAsync(false);
            if (permissions == false)
            {
                return;
            }

            var chat = CacheService.GetChat(chatId);
            if (chat == null || chat.VoiceChat.GroupCallId != 0)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CreateVoiceChat(chat.Id));
            if (response is GroupCallId groupCallId)
            {
                await JoinAsyncInternal(chat, groupCallId.Id, false);
            }
        }

        private async Task JoinAsyncInternal(Chat chat, int groupCallId, bool pickAlias)
        {
            var activeCall = _call;
            if (activeCall != null)
            {
                if (activeCall.Id == groupCallId)
                {
                    await ShowAsync(activeCall, _manager);
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

            var alias = chat.VoiceChat?.DefaultParticipantAlias;
            if (alias == null || pickAlias)
            {
                alias = await PickAliasAsync(chat);
            }

            var response = await ProtoService.SendAsync(new GetGroupCall(groupCallId));
            if (response is GroupCall groupCall)
            {
                _chat = chat;
                _call = groupCall;

                var descriptor = new VoipGroupDescriptor
                {
                    AudioInputId = await _inputWatcher.GetAndUpdateAsync(),
                    AudioOutputId = await _outputWatcher.GetAndUpdateAsync()
                };

                _inputWatcher.Start();
                _outputWatcher.Start();

                _manager = new VoipGroupManager(descriptor);
                _manager.NetworkStateUpdated += OnNetworkStateUpdated;
                _manager.AudioLevelsUpdated += OnAudioLevelsUpdated;
                _manager.FrameRequested += OnFrameRequested;

                await ShowAsync(groupCall, _manager);

                Rejoin(groupCallId, alias);
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

            var alias = await PickAliasAsync(chat);
            if (alias != null)
            {
                Rejoin(call.Id, alias);
            }
        }

        private async Task<MessageSender> PickAliasAsync(Chat chat)
        {
            var available = await CanChooseAliasAsync(chat.Id);
            if (available && _availableAliases != null)
            {
                var dialog = new VoiceChatAliasesPopup(ProtoService, chat, _availableAliases.Senders.ToArray());
                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    return dialog.SelectedSender;
                }
            }

            return null;
        }

        private void Rejoin(int groupCallId, MessageSender alias)
        {
            var leave = _alias != null && _alias.Equals(alias);

            _alias = alias;

            _manager?.SetConnectionMode(VoipGroupConnectionMode.None, false);
            _manager?.EmitJoinPayload(async (ssrc, payload) =>
            {
                if (leave)
                {
                    await ProtoService.SendAsync(new LeaveGroupCall(groupCallId));
                }

                Participants?.Dispose();
                Participants = new GroupCallParticipantsCollection(ProtoService, Aggregator, groupCallId);

                var response = await ProtoService.SendAsync(new JoinGroupCall(groupCallId, alias, payload, ssrc, true, string.Empty));
                if (response is GroupCallJoinResponseWebrtc data)
                {
                    _source = ssrc;
                    _manager.SetConnectionMode(VoipGroupConnectionMode.Rtc, true);
                    _manager.SetJoinResponsePayload(data, null);

                    ProtoService.Send(new LoadGroupCallParticipants(groupCallId, 100));
                }
                else if (response is GroupCallJoinResponseStream)
                {
                    _source = ssrc;
                    _manager.SetConnectionMode(VoipGroupConnectionMode.Broadcast, true);

                    ProtoService.Send(new LoadGroupCallParticipants(groupCallId, 100));
                }
            });
        }

        private async void OnFrameRequested(VoipGroupManager sender, FrameRequestedEventArgs args)
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
                args.Ready(time, response.Value + stamp, result as FilePart);
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

        private void OnAudioLevelsUpdated(VoipGroupManager sender, IReadOnlyDictionary<int, KeyValuePair<float, bool>> levels)
        {
            foreach (var level in levels)
            {
                ProtoService.Send(new SetGroupCallParticipantIsSpeaking(_call.Id, level.Key == 0 ? _source : level.Key, level.Value.Value));
            }
        }

        public async void Discard()
        {
            await DisposeAsync(true);
        }

        private Task DisposeAsync(bool? discard)
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

            _alias = null;
            _availableAliases = null;
            _availableAliasesTask = null;

            Participants = null;

            return Task.Run(() =>
            {
                if (_manager != null)
                {
                    _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                    _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;

                    _manager.Dispose();
                    _manager = null;

                    _source = 0;
                }
            });
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

        public async void Handle(UpdateGroupCall update)
        {
            //using (_updateLock.WaitAsync())
            {
                if (_call?.Id == update.GroupCall.Id)
                {
                    _call = update.GroupCall;

                    var callPage = _callPage;
                    if (callPage != null)
                    {
                        await callPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            callPage.Update(update.GroupCall);
                        });
                    }

                    if (update.GroupCall.NeedRejoin)
                    {
                        Rejoin(update.GroupCall.Id, _alias);
                    }
                }
            }

            Aggregator.Publish(new UpdateCallDialog(update.GroupCall));
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            var manager = _manager;
            if (manager == null)
            {
                return;
            }

            manager.SetVolume(update.Participant.Source, update.Participant.VolumeLevel / 10000d);
        }

        public Chat Chat => _chat;
        public GroupCall Call => _call;
        public int Source => _source;

        public bool IsConnected => _isConnected;

        public void Show()
        {
            if (_call != null)
            {
                ShowAsync(_call, _manager);
            }
            else
            {
                //Aggregator.Publish(new UpdateCallDialog(null, false));
            }
        }

        private async Task ShowAsync(GroupCall call, VoipGroupManager controller)
        {
            using (await _updateLock.WaitAsync())
            {
                if (_callPage == null)
                {
                    var parameters = new ViewServiceParams
                    {
                        Title = Strings.Resources.VoipGroupVoiceChat,
                        Width = 380,
                        Height = 580,
                        PersistentId = "VoiceChat",
                        Content = control => _callPage = new GroupCallPage(ProtoService, CacheService, Aggregator, this)
                    };

                    _callLifetime = await _viewService.OpenAsync(parameters);
                    _callLifetime.Closed += ApplicationView_Released;
                    _callLifetime.Released += ApplicationView_Released;

                    //Aggregator.Publish(new UpdateCallDialog(call, true));
                }
            }
        }

        private async void Hide()
        {
            using (await _updateLock.WaitAsync())
            {
                //Aggregator.Publish(new UpdateCallDialog(_call, true));

                _callPage = null;

                var lifetime = _callLifetime;
                if (lifetime != null)
                {
                    _callLifetime = null;

                    lifetime.Dispatcher.Dispatch(() =>
                    {
                        if (lifetime.Window.Content is GroupCallPage callPage)
                        {
                            callPage.Dispose();
                        }

                        lifetime.Window.Close();
                    });
                }
            }
        }

        private async void ApplicationView_Released(object sender, EventArgs e)
        {
            using (await _updateLock.WaitAsync())
            {
                _callPage = null;
                _callLifetime = null;
                //Aggregator.Publish(new UpdateCallDialog(_call, false));
            }
        }
    }
}
