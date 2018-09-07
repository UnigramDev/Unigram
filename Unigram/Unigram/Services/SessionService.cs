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
            if (Settings.Notifications.IncludeMutedChats)
            {
                Execute.BeginOnUIThread(() => UnreadCount = update.UnreadCount, () => _unreadCount = update.UnreadCount);
            }
            else
            {
                Execute.BeginOnUIThread(() => UnreadCount = update.UnreadUnmutedCount, () => _unreadCount = update.UnreadUnmutedCount);
            }
        }

        #region Lifecycle

        public void Handle(UpdateAuthorizationState update)
        {
            if (update.AuthorizationState is AuthorizationStateClosed)
            {
                _lifecycleService.Destroy(this);
            }
            else if (update.AuthorizationState is AuthorizationStateWaitPhoneNumber && !_isActive && _lifecycleService.Items.Count > 1)
            {
                ProtoService.Send(new Destroy());
            }

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                window.Handle(this, update);
            }
        }

        public void Handle(UpdateConnectionState update)
        {
            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                window.Handle(this, update);
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
