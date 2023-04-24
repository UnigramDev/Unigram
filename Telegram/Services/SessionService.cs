//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.ComponentModel;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Services
{
    public interface ISessionService : INotifyPropertyChanged
    {
        int Id { get; }
        long UserId { get; }

        bool IsActive { get; set; }

        int UnreadCount { get; }



        IClientService ClientService { get; }
        ISettingsService Settings { get; }
        IEventAggregator Aggregator { get; }

        Task<BaseObject> SetAuthenticationPhoneNumberAsync(SetAuthenticationPhoneNumber function);
    }

    public class SessionService : TLViewModelBase
        , ISessionService
    //, IHandle<UpdateUnreadMessageCount>
    //, IHandle<UpdateUnreadChatCount>
    //, IHandle<UpdateAuthorizationState>
    {
        private readonly ILifetimeService _lifetimeService;
        private readonly int _id;

        public SessionService(int session, bool selected, IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILifetimeService lifecycleService)
            : base(clientService, settingsService, aggregator)
        {
            _lifetimeService = lifecycleService;
            _id = session;

            Subscribe();

            IsActive = selected;

            var unreadCount = ClientService.GetUnreadCount(new ChatListMain());
            Handle(unreadCount.UnreadChatCount);
            Handle(unreadCount.UnreadMessageCount);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUnreadMessageCount>(this, Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateAuthorizationState>(Handle);
        }

        public int Id => _id;
        public long UserId => ClientService.Options.MyId;

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            private set => Set(ref _unreadCount, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                //Set(ref _isActive, value);
                _isActive = value;

                if (!value)
                {
                    ClientService.Options.Online = value;
                }
            }
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == ClientService.Options.MyId)
            {
                _lifetimeService.Update();
            }
        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            if (!Settings.Notifications.CountUnreadMessages)
            {
                return;
            }

            if (update.ChatList is ChatListMain)
            {
                if (Settings.Notifications.IncludeMutedChats)
                {
                    BeginOnUIThread(() => UnreadCount = update.UnreadCount, () => _unreadCount = update.UnreadCount);
                }
                else
                {
                    BeginOnUIThread(() => UnreadCount = update.UnreadUnmutedCount, () => _unreadCount = update.UnreadUnmutedCount);
                }
            }
        }

        public void Handle(UpdateUnreadChatCount update)
        {
            if (Settings.Notifications.CountUnreadMessages)
            {
                return;
            }

            if (update.ChatList is ChatListMain)
            {
                if (Settings.Notifications.IncludeMutedChats)
                {
                    BeginOnUIThread(() => UnreadCount = update.UnreadCount, () => _unreadCount = update.UnreadCount);
                }
                else
                {
                    BeginOnUIThread(() => UnreadCount = update.UnreadUnmutedCount, () => _unreadCount = update.UnreadUnmutedCount);
                }
            }
        }

        #region Lifecycle

        private bool _loggingOut;
        private bool _continueOnLogOut;
        private SetAuthenticationPhoneNumber _continueOnLogOutAction;
        private TaskCompletionSource<BaseObject> _continueResult;

        public Task<BaseObject> SetAuthenticationPhoneNumberAsync(SetAuthenticationPhoneNumber function)
        {
            _loggingOut = false;
            _continueOnLogOut = true;
            _continueOnLogOutAction = function;
            _continueResult = new TaskCompletionSource<BaseObject>();

            ClientService.Send(new LogOut());

            return _continueResult.Task;
        }

        private async void ContinueOnLogOut()
        {
            var function = _continueOnLogOutAction;
            if (function == null)
            {
                return;
            }

            var source = _continueResult;
            if (source == null)
            {
                return;
            }

            _continueOnLogOut = false;
            _continueOnLogOutAction = null;
            _continueResult = null;

            var response = await ClientService.SendAsync(function);
            source.SetResult(response);
        }

        public void Handle(UpdateAuthorizationState update)
        {
            if (update.AuthorizationState is AuthorizationStateLoggingOut && !_continueOnLogOut)
            {
                _loggingOut = true;
            }
            else if (update.AuthorizationState is AuthorizationStateClosed)
            {
                if (_loggingOut)
                {
                    _loggingOut = false;
                    _lifetimeService.Destroy(this);
                }
                else if (_continueOnLogOut)
                {
                    ClientService.TryInitialize();
                }
            }
            else if (update.AuthorizationState is AuthorizationStateWaitPhoneNumber && _continueOnLogOut)
            {
                ContinueOnLogOut();
            }
            else if ((update.AuthorizationState is AuthorizationStateWaitPhoneNumber || update.AuthorizationState is AuthorizationStateWaitOtherDeviceConfirmation) && !_isActive && _lifetimeService.Items.Count > 1)
            {
                ClientService.Send(new Destroy());
            }
            else
            {
                _loggingOut = false;
            }

            //if (update.AuthorizationState is AuthorizationStateReady)
            //{
            //    _lifetimeService.Register(this);
            //}
            //else
            //{
            //    _lifetimeService.Unregister(this);
            //}

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                window.Handle(this, update);
            }
        }

        #endregion
    }
}
