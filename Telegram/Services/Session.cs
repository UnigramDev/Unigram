//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Host;
using Windows.System;

namespace Telegram.Services
{
    public interface ISession : INotifyPropertyChanged
    {
        int Id { get; }
        long UserId { get; }

        bool IsActive { get; set; }

        int UnreadCount { get; }
        bool IsUnmuted { get; }
        bool ShowCount { get; }

        IClientService ClientService { get; }
        ISettingsService Settings { get; }
        IEventAggregator Aggregator { get; }

        bool TryResolve<T>(out T result);
        T Resolve<T>();
        T Resolve<T, TDelegate>(TDelegate delegato) where T : IDelegable<TDelegate> where TDelegate : IViewModelDelegate;

        Task<Object> SetAuthenticationPhoneNumberAsync(SetAuthenticationPhoneNumber function);
        void RequestQrCodeAuthentication(IList<long> otherUserIds);
    }

    // TODO: Name collides with Td.Api.Session.
    //       Find a better name for this class and ISession.
    public partial class SessionImpl : BindableBase, ISession
    {
        public IClientService ClientService => _clientService;

        public ISettingsService Settings => _settingsService;

        public IEventAggregator Aggregator => _eventAggregator;

        private void Initialize(bool active)
        {
            _unreadCount = new DebouncedProperty<int>(200, UpdateUnreadCount, useBackgroundThread: true);

            Aggregator.Subscribe<UpdateUnreadMessageCount>(this, Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateAuthorizationState>(Handle);

            IsActive = active;

            var unreadCount = ClientService.GetUnreadCount(new ChatListMain());
            Handle(unreadCount.UnreadChatCount);
            Handle(unreadCount.UnreadMessageCount);
        }

        public bool TryResolve<T>(out T result)
        {
            result = Resolve<T>();
            return result != null;
        }

        public T Resolve<T, TDelegate>(TDelegate delegato) where T : IDelegable<TDelegate> where TDelegate : IViewModelDelegate
        {
            var result = Resolve<T>();
            result?.Delegate = delegato;

            return result;
        }


        public int Id => _id;
        public long UserId => ClientService.Options.MyId;

        private DebouncedProperty<int> _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            private set => _unreadCount.Set(value);
        }

        public bool IsUnmuted => !Settings.Notifications.IncludeMutedChats;

        public bool ShowCount => UnreadCount > 0;

        private void UpdateUnreadCount(int value)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(nameof(UnreadCount));
                RaisePropertyChanged(nameof(IsUnmuted));
                RaisePropertyChanged(nameof(ShowCount));
            });
        }

        public virtual void BeginOnUIThread(DispatcherQueueHandler action)
        {
            var dispatcher = WindowContext.Main?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Dispatch(action);
            }
            else
            {
                try
                {
                    action();
                }
                catch { }
            }
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

                    // TODO: consider restarting the instance to free some memory if:
                    // No secondary windows are open, no calls are in progress, no audio is being played.
                    //ClientService.Close(true);
                }
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
                    UnreadCount = update.UnreadCount;
                }
                else
                {
                    UnreadCount = update.UnreadUnmutedCount;
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
                    UnreadCount = update.UnreadCount;
                }
                else
                {
                    UnreadCount = update.UnreadUnmutedCount;
                }
            }
        }

        #region Lifecycle

        private bool _loggingOut;
        private bool _continueOnLogOut;
        private Function _continueOnLogOutAction;
        private TaskCompletionSource<Object> _continueResult;

        public Task<Object> SetAuthenticationPhoneNumberAsync(SetAuthenticationPhoneNumber function)
        {
            _loggingOut = false;
            _continueOnLogOut = true;
            _continueOnLogOutAction = function;
            _continueResult = new TaskCompletionSource<Object>();

            ClientService.Send(new LogOut());

            return _continueResult.Task;
        }

        public void RequestQrCodeAuthentication(IList<long> otherUserIds)
        {
            _loggingOut = false;
            _continueOnLogOut = true;
            _continueOnLogOutAction = new RequestQrCodeAuthentication(otherUserIds);
            _continueResult = new TaskCompletionSource<Object>();

            ClientService.Send(new LogOut());
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

                WindowContext.ForEach(window =>
                {
                    if (window.Content is StandalonePage page && page.NavigationService?.Session == this)
                    {
                        _ = window.ConsolidateAsync();
                    }
                    else if (window.Content is RootPage root && root.NavigationService?.Session == this)
                    {
                        root.NavigationService.Block();
                        ContentPopup.Block(root.NavigationService.XamlRoot);
                    }
                });
            }
            else if (update.AuthorizationState is AuthorizationStateClosed)
            {
                if (_loggingOut)
                {
                    _loggingOut = false;
                    _lifetimeService.Destroy(this);

                    Settings.Clear();
                    Settings.PasscodeLock.Clear();
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
            else if (update.AuthorizationState is AuthorizationStateWaitPhoneNumber or AuthorizationStateWaitOtherDeviceConfirmation && !_isActive && _lifetimeService.Count > 1)
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

            if (IsActive)
            {
                WindowContext.ForEach(window =>
                {
                    var root = window.NavigationServices.FirstOrDefault(x => x.Session == this && x.FrameFacade.FrameId == $"{Id}") as TLRootNavigationService;
                    root?.Handle(update);
                });
            }
        }

        #endregion
    }
}
