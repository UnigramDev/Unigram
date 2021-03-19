using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services.Updates;
using Unigram.Services.ViewService;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IGroupCallService : IHandle<UpdateGroupCall>
    {
        string CurrentAudioInput { get; set; }
        string CurrentAudioOutput { get; set; }

        VoipGroupManager Manager { get; }

        Chat Chat { get; }
        GroupCall Call { get; }
        int Source { get; }
        bool IsConnected { get; }

        void Show();

        Task JoinAsync(long chatId);
        void Leave();

        Task CreateAsync(long chatId);
        void Discard();
    }

    public class GroupCallService : TLViewModelBase, IGroupCallService
    {
        private readonly IViewService _viewService;

        private readonly MediaDeviceWatcher _inputWatcher;
        private readonly MediaDeviceWatcher _outputWatcher;

        private readonly DisposableMutex _updateLock = new DisposableMutex();

        private Chat _chat;

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

        public async Task JoinAsync(long chatId)
        {
            var permissions = await MediaDeviceWatcher.CheckAccessAsync(false);
            if (permissions == false)
            {
                return;
            }

            var chat = CacheService.GetChat(chatId);
            if (chat == null || chat.VoiceChat == null)
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

            await JoinAsyncInternal(chat, chat.VoiceChat.GroupCallId);
        }

        public void Leave()
        {
            Task.Run(() =>
            {
                if (_manager != null)
                {
                    _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                    _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;

                    _manager.Dispose();
                    _manager = null;
                }

                if (_call != null)
                {
                    ProtoService.Send(new LeaveGroupCall(_call.Id));
                    _call = null;
                    _chat = null;
                }
            });
        }

        public async Task CreateAsync(long chatId)
        {
            var permissions = await MediaDeviceWatcher.CheckAccessAsync(false);
            if (permissions == false)
            {
                return;
            }

            var chat = CacheService.GetChat(chatId);
            if (chat == null || chat.VoiceChat != null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CreateVoiceChat(chat.Id));
            if (response is GroupCallId groupCallId)
            {
                await JoinAsyncInternal(chat, groupCallId.Id);
            }
        }

        private async Task JoinAsyncInternal(Chat chat, int groupCallId)
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

                    if (_manager != null)
                    {
                        _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                        _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;

                        _manager.Dispose();
                        _manager = null;
                    }

                    await ProtoService.SendAsync(new LeaveGroupCall(_call.Id));
                    _call = null;
                    _chat = null;
                }
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

                _manager.EmitJoinPayload(async (ssrc, payload) =>
                {
                    var response = await ProtoService.SendAsync(new JoinGroupCall(groupCallId, null, payload, ssrc, true, string.Empty));
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

        public void Discard()
        {
            Task.Run(() =>
            {
                if (_manager != null)
                {
                    _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                    _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;

                    _manager.Dispose();
                    _manager = null;
                }

                if (_call != null)
                {
                    ProtoService.Send(new DiscardGroupCall(_call.Id));
                    _call = null;
                    _chat = null;
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
                        _manager.EmitJoinPayload(async (ssrc, payload) =>
                        {
                            var response = await ProtoService.SendAsync(new JoinGroupCall(update.GroupCall.Id, null, payload, ssrc, true, string.Empty));
                            if (response is GroupCallJoinResponseWebrtc data)
                            {
                                _source = ssrc;
                                _manager.SetJoinResponsePayload(data, null);
                            }
                        });
                    }
                }
            }

            Aggregator.Publish(new UpdateCallDialog(update.GroupCall));
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

                var callPage = _callPage;
                if (callPage == null)
                {
                    return;
                }

                await callPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (controller != null)
                    {
                        callPage.Connect(controller);
                    }

                    callPage.Update(call);
                });
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
