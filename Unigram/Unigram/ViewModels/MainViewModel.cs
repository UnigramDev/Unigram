using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.Common;
using Unigram.Common.Dialogs;
using Unigram.Controls.Views;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Views;
using Windows.Media.Playback;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using libtgvoip;
using Windows.Storage;
using System.Linq;
using Unigram.Controls;
using Windows.UI.Xaml;

namespace Unigram.ViewModels
{
    public class MainViewModel : TLMultipleViewModelBase, IHandle<UpdateServiceNotification>, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateCall>
    {
        private readonly INotificationsService _pushService;
        private readonly IVibrationService _vibrationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly IPasscodeService _passcodeService;
        private readonly ILifetimeService _lifetimeService;
        private readonly ISessionService _sessionService;

        private readonly ConcurrentDictionary<int, InputTypingManager> _typingManagers;
        private readonly ConcurrentDictionary<int, InputTypingManager> _chatTypingManagers;

        public bool Refresh { get; set; }

        public MainViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService pushService, IVibrationService vibrationService, ILiveLocationService liveLocationService, IContactsService contactsService, IPasscodeService passcodeService, ILifetimeService lifecycle, ISessionService session)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _pushService = pushService;
            _vibrationService = vibrationService;
            _liveLocationService = liveLocationService;
            _passcodeService = passcodeService;
            _lifetimeService = lifecycle;
            _sessionService = session;

            _typingManagers = new ConcurrentDictionary<int, InputTypingManager>();
            _chatTypingManagers = new ConcurrentDictionary<int, InputTypingManager>();

            //Dialogs = new DialogCollection(protoService, cacheService);
            Chats = new ChatsViewModel(protoService, cacheService, settingsService, aggregator);
            Contacts = new ContactsViewModel(protoService, cacheService, settingsService, aggregator, contactsService);
            Calls = new CallsViewModel(protoService, cacheService, settingsService, aggregator);
            Settings = new SettingsViewModel(protoService, cacheService, settingsService, aggregator, pushService, contactsService);

            ChildViewModels.Add(Chats);
            ChildViewModels.Add(Contacts);
            ChildViewModels.Add(Calls);
            ChildViewModels.Add(Settings);

            aggregator.Subscribe(this);

            LiveLocationCommand = new RelayCommand(LiveLocationExecute);
            StopLiveLocationCommand = new RelayCommand(StopLiveLocationExecute);

            ReturnToCallCommand = new RelayCommand(ReturnToCallExecute);
        }

        public ILifetimeService Lifetime => _lifetimeService;
        public ISessionService Session => _sessionService;

        public ILiveLocationService LiveLocation => _liveLocationService;
        public IPasscodeService Passcode => _passcodeService;

        public RelayCommand LiveLocationCommand { get; }
        private async void LiveLocationExecute()
        {
            await new LiveLocationsView().ShowQueuedAsync();
        }

        public RelayCommand StopLiveLocationCommand { get; }
        private void StopLiveLocationExecute()
        {
            _liveLocationService.StopTracking();
        }

        private int _unreadMutedCount;
        public int UnreadMutedCount
        {
            get
            {
                return _unreadMutedCount;
            }
            set
            {
                Set(ref _unreadMutedCount, value);
            }
        }

        private int _unreadUnmutedCount;
        public int UnreadUnmutedCount
        {
            get
            {
                return _unreadUnmutedCount;
            }
            set
            {
                Set(ref _unreadUnmutedCount, value);
            }
        }

        #region Typing

        //public void Handle(TLUpdateUserTyping update)
        //{
        //    var user = CacheService.GetUser(update.UserId) as TLUser;
        //    if (user == null)
        //    {
        //        return;
        //    }

        //    if (user.IsSelf)
        //    {
        //        return;
        //    }

        //    //var dialog = CacheService.GetDialog(user.ToPeer());
        //    //if (dialog == null)
        //    //{
        //    //    return;
        //    //}

        //    //_typingManagers.TryGetValue(update.UserId, out InputTypingManager typingManager);
        //    //if (typingManager == null)
        //    //{
        //    //    typingManager = new InputTypingManager(users =>
        //    //    {
        //    //        dialog.TypingSubtitle = DialogViewModel.GetTypingString(user.ToPeer(), users, CacheService.GetUser, null);
        //    //        dialog.IsTyping = true;
        //    //    },
        //    //    () =>
        //    //    {
        //    //        dialog.TypingSubtitle = null;
        //    //        dialog.IsTyping = false;
        //    //    });

        //    //    _typingManagers[update.UserId] = typingManager;
        //    //}

        //    //var action = update.Action;
        //    //if (action is TLSendMessageCancelAction)
        //    //{
        //    //    typingManager.RemoveTypingUser(update.UserId);
        //    //    return;
        //    //}

        //    //typingManager.AddTypingUser(update.UserId, action);
        //}

        //public void Handle(TLUpdateChatUserTyping update)
        //{
        //    var chat = CacheService.GetChat(update.ChatId) as TLChatBase;
        //    if (chat == null)
        //    {
        //        return;
        //    }

        //    //var dialog = CacheService.GetDialog(chat.ToPeer());
        //    //if (dialog == null)
        //    //{
        //    //    return;
        //    //}

        //    //_typingManagers.TryGetValue(update.ChatId, out InputTypingManager typingManager);
        //    //if (typingManager == null)
        //    //{
        //    //    typingManager = new InputTypingManager(users =>
        //    //    {
        //    //        dialog.TypingSubtitle = DialogViewModel.GetTypingString(chat.ToPeer(), users, CacheService.GetUser, null);
        //    //        dialog.IsTyping = true;
        //    //    },
        //    //    () =>
        //    //    {
        //    //        dialog.TypingSubtitle = null;
        //    //        dialog.IsTyping = false;
        //    //    });

        //    //    _typingManagers[update.ChatId] = typingManager;
        //    //}

        //    //var action = update.Action;
        //    //if (action is TLSendMessageCancelAction)
        //    //{
        //    //    typingManager.RemoveTypingUser(update.UserId);
        //    //    return;
        //    //}

        //    //typingManager.AddTypingUser(update.UserId, action);
        //}

        #endregion

        private Call _call;
        private VoIPControllerWrapper _controller;

        public void Handle(UpdateCall update)
        {
            _call = update.Call;

            if (update.Call.State is CallStateReady ready)
            {
                var user = CacheService.GetUser(update.Call.UserId);
                if (user == null)
                {
                    return;
                }

                VoIPControllerWrapper.UpdateServerConfig(ready.Config);

                var logFile = ApplicationData.Current.LocalFolder.Path + "\\tgvoip.logFile.txt";
                var statsDumpFile = ApplicationData.Current.LocalFolder.Path + "\\tgvoip.statsDump.txt";

                var call_packet_timeout_ms = CacheService.GetOption<OptionValueInteger>("call_packet_timeout_ms");
                var call_connect_timeout_ms = CacheService.GetOption<OptionValueInteger>("call_connect_timeout_ms");

                if (_controller != null)
                {
                    _controller.Dispose();
                    _controller = null;
                }

                _controller = new VoIPControllerWrapper();
                _controller.SetConfig(call_packet_timeout_ms.Value / 1000.0, call_connect_timeout_ms.Value / 1000.0, base.Settings.UseLessData, true, true, true, logFile, statsDumpFile);

                BeginOnUIThread(() =>
                {
                    ShowCall(update.Call, _controller);
                });

                var p2p = base.Settings.PeerToPeerMode == 0 || (base.Settings.PeerToPeerMode == 1 && user.OutgoingLink is LinkStateIsContact);
                var endpoints = new Endpoint[ready.Connections.Count];

                for (int i = 0; i < endpoints.Length; i++)
                {
                    endpoints[i] = new Endpoint { id = ready.Connections[i].Id, ipv4 = ready.Connections[i].Ip, ipv6 = ready.Connections[i].Ipv6, peerTag = ready.Connections[i].PeerTag.ToArray(), port = (ushort)ready.Connections[i].Port };
                }

                _controller.SetEncryptionKey(ready.EncryptionKey.ToArray(), update.Call.IsOutgoing);
                _controller.SetPublicEndpoints(endpoints, ready.Protocol.UdpP2p && p2p);
                _controller.Start();
                _controller.Connect();
            }
            else if (update.Call.State is CallStateDiscarded)
            {
                _controller?.Dispose();
                _controller = null;
            }

            BeginOnUIThread(() =>
            {
                switch (update.Call.State)
                {
                    case CallStateDiscarded discarded:
                    case CallStateError error:
                        HideCall();
                        break;
                    default:
                        ShowCall(update.Call, null);
                        break;
                }
            });
        }

        private PhoneCallPage _callPage;
        private ContentDialogBase _callDialog;

        private void ShowCall(Call call, VoIPControllerWrapper controller)
        {
            if (_callPage == null)
            {
                _callPage = new PhoneCallPage(ProtoService, CacheService, false);

                _callDialog = new ContentDialogBase();
                _callDialog.HorizontalAlignment = HorizontalAlignment.Stretch;
                _callDialog.VerticalAlignment = VerticalAlignment.Stretch;
                _callDialog.Content = _callPage;
                _callDialog.IsOpen = true;
            }

            if (controller != null)
            {
                _callPage.Connect(controller);
            }

            _callPage.Update(call);
        }

        private void HideCall()
        {
            if (_callPage != null)
            {
                _callPage.Dispose();
                _callPage = null;
            }

            if (_callDialog != null)
            {
                _callDialog.IsOpen = false;
                _callDialog = null;
            }
        }

        public RelayCommand ReturnToCallCommand { get; }
        private void ReturnToCallExecute()
        {
            ShowCall(_call, _controller);
            
            if (_callDialog != null)
            {
                _callDialog.IsOpen = true;
            }
        }

        public void Handle(UpdateServiceNotification update)
        {

        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            BeginOnUIThread(() =>
            {
                UnreadUnmutedCount = update.UnreadUnmutedCount;
                UnreadMutedCount = update.UnreadCount - update.UnreadUnmutedCount;
            });
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() => _pushService.RegisterAsync());
            }

            //BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //BeginOnUIThread(() => Settings.OnNavigatedToAsync(parameter, mode, state));
            //Dispatch(() => Dialogs.LoadFirstSlice());
            //Dispatch(() => Contacts.getTLContacts());
            //Dispatch(() => Contacts.GetSelfAsync());

            UnreadMutedCount = CacheService.UnreadCount - CacheService.UnreadUnmutedCount;

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public ChatsViewModel Chats { get; private set; }
        public ContactsViewModel Contacts { get; private set; }
        public CallsViewModel Calls { get; private set; }
        public SettingsViewModel Settings { get; private set; }
    }

    public class YoloTimer
    {
        private Timer _timer;
        private TimerCallback _callback;
        private DateTime? _start;

        public YoloTimer(TimerCallback callback, object state)
        {
            _callback = callback;
            _timer = new Timer(OnCallback, state, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnCallback(object state)
        {
            _start = null;
            _callback(state);
        }

        public void CallOnce(int seconds)
        {
            _start = DateTime.Now;
            _timer.Change(seconds * 1000, Timeout.Infinite);
        }

        public bool IsActive
        {
            get
            {
                return _start.HasValue;
            }
        }

        public TimeSpan RemainingTime
        {
            get
            {
                if (_start.HasValue)
                {
                    return DateTime.Now - _start.Value;
                }

                return TimeSpan.Zero;
            }
        }
    }
}
