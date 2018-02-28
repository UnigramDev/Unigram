using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Template10.Common;
using Unigram.Common;
using Unigram.Common.Dialogs;
using Unigram.Controls.Views;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Views;
using Windows.Media.Playback;
using Windows.UI.Xaml.Navigation;
using TdWindows;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase, IHandle<UpdateServiceNotification>
    {
        private readonly INotificationsService _pushService;
        private readonly IVibrationService _vibrationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly IPasscodeService _passcodeService;

        private readonly ConcurrentDictionary<int, InputTypingManager> _typingManagers;
        private readonly ConcurrentDictionary<int, InputTypingManager> _chatTypingManagers;

        public bool Refresh { get; set; }

        public MainViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, INotificationsService pushService, IVibrationService vibrationService, ILiveLocationService liveLocationService, IContactsService contactsService, IPasscodeService passcodeService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;
            _vibrationService = vibrationService;
            _liveLocationService = liveLocationService;
            _passcodeService = passcodeService;

            _typingManagers = new ConcurrentDictionary<int, InputTypingManager>();
            _chatTypingManagers = new ConcurrentDictionary<int, InputTypingManager>();

            //Dialogs = new DialogCollection(protoService, cacheService);
            Chats = new ChatsViewModel(protoService, cacheService, aggregator);
            Contacts = new ContactsViewModel(protoService, cacheService, aggregator, contactsService);
            Calls = new CallsViewModel(protoService, cacheService, aggregator);

            aggregator.Subscribe(this);

            LiveLocationCommand = new RelayCommand(LiveLocationExecute);
            StopLiveLocationCommand = new RelayCommand(StopLiveLocationExecute);
        }

        public override IDispatcherWrapper Dispatcher
        {
            get => base.Dispatcher;
            set
            {
                base.Dispatcher = value;
                Chats.Dispatcher = value;
                Contacts.Dispatcher = value;
                Calls.Dispatcher = value;
            }
        }

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

        public void Handle(UpdateServiceNotification update)
        {

        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() => _pushService.RegisterAsync());
            }

            BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //Dispatch(() => Dialogs.LoadFirstSlice());
            //Dispatch(() => Contacts.getTLContacts());
            //Dispatch(() => Contacts.GetSelfAsync());

            return Task.CompletedTask;
        }

        public ChatsViewModel Chats { get; private set; }
        public ContactsViewModel Contacts { get; private set; }
        public CallsViewModel Calls { get; private set; }
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
