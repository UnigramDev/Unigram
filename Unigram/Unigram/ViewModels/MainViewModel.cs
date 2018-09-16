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
    public class MainViewModel : TLMultipleViewModelBase, IHandle<UpdateServiceNotification>, IHandle<UpdateUnreadMessageCount>
    {
        private readonly INotificationsService _pushService;
        private readonly IVibrationService _vibrationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly IPasscodeService _passcodeService;
        private readonly ILifetimeService _lifetimeService;
        private readonly ISessionService _sessionService;
        private readonly IVoIPService _voipService;

        private readonly ConcurrentDictionary<int, InputTypingManager> _typingManagers;
        private readonly ConcurrentDictionary<int, InputTypingManager> _chatTypingManagers;

        public bool Refresh { get; set; }

        public MainViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService pushService, IVibrationService vibrationService, ILiveLocationService liveLocationService, IContactsService contactsService, IPasscodeService passcodeService, ILifetimeService lifecycle, ISessionService session, IVoIPService voipService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _pushService = pushService;
            _vibrationService = vibrationService;
            _liveLocationService = liveLocationService;
            _passcodeService = passcodeService;
            _lifetimeService = lifecycle;
            _sessionService = session;
            _voipService = voipService;

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
            ChildViewModels.Add(_voipService as TLViewModelBase);

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

        public RelayCommand ReturnToCallCommand { get; }
        private void ReturnToCallExecute()
        {
            _voipService.Show();
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
