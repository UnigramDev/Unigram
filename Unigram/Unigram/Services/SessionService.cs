using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface ISessionService : INotifyPropertyChanged
    {
        int Id { get; }
        int UserId { get; }

        bool IsActive { get; set; }

        int UnreadCount { get; }



        IProtoService ProtoService { get; }
        IEventAggregator Aggregator { get; }
    }

    public class SessionService : TLViewModelBase, ISessionService, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateAuthorizationState>, IHandle<UpdateConnectionState>
    {
        private readonly ILifecycleService _lifecycleService;
        private readonly int _id;

        public SessionService(int session, bool selected, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILifecycleService lifecycleService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _lifecycleService = lifecycleService;
            _id = session;

            aggregator.Subscribe(this);

            IsActive = selected;
            UnreadCount = ProtoService.UnreadCount;
        }

        public int Id => _id;
        public int UserId => ProtoService.GetMyId();

        private int _unreadCount;
        public int UnreadCount
        {
            get
            {
                return _unreadCount;
            }
            private set
            {
                Set(ref _unreadCount, value);
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                //Set(ref _isActive, value);
                _isActive = value;
            }
        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            Execute.BeginOnUIThread(() => UnreadCount = update.UnreadCount, () => _unreadCount = update.UnreadCount);
        }

        #region Lifecycle

        public void Handle(UpdateAuthorizationState update)
        {
            //if (update.AuthorizationState is AuthorizationStateClosed)
            //{
            //    var active = _isActive;
            //    var session = _lifecycleService.Remove(this, null);
            //    if (active)
            //    {
            //        BeginOnUIThread(() =>
            //        {
            //            if (Window.Current.Content is RootPage root)
            //            {
            //                root.Switch(session);
            //            }
            //        });
            //    }
            //}

            if (update.AuthorizationState is AuthorizationStateClosed)
            {
                _lifecycleService.Closed(this);
            }

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                window.Handle(this, update);
            }
        }

        public void Handle(UpdateConnectionState update)
        {
            if (_isActive)
            {
                foreach (var window in WindowContext.ActiveWrappers)
                {
                    foreach (var service in window.NavigationServices)
                    {
                        if (service.SessionId == _id)
                        {

                        }
                    }
                }
            }
        }

        #endregion

        protected override void BeginOnUIThread(Action action)
        {
            // This is somehow needed because this viewmodel requires a Dispatcher
            // in some situations where base one might be null.
            Execute.BeginOnUIThread(action);
        }
    }
}
