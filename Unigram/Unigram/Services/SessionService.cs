using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;

namespace Unigram.Services
{
    public interface ISessionService : INotifyPropertyChanged
    {
        int Id { get; }
        int UserId { get; }

        bool IsActive { get; set; }

        void Subscribe(WindowContext window);
        void Unsubscribe(WindowContext window);

        int UnreadCount { get; }



        IProtoService ProtoService { get; }
        IEventAggregator Aggregator { get; }
    }

    public class SessionService : TLViewModelBase, ISessionService, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateAuthorizationState>, IHandle<UpdateConnectionState>
    {
        private readonly Dictionary<long, WindowContext> _windows = new Dictionary<long, WindowContext>();

        public SessionService(bool selected, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            WindowContext.Subscribe(this);
            aggregator.Subscribe(this);

            IsActive = selected;
            UnreadCount = ProtoService.UnreadCount;
        }

        public int Id => ProtoService.SessionId;
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
                Set(ref _isActive, value);
            }
        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            Execute.BeginOnUIThread(() => UnreadCount = update.UnreadCount, () => _unreadCount = update.UnreadCount);
        }

        #region Lifecycle

        public void Subscribe(WindowContext window)
        {
            _windows[window.Id] = window;
        }

        public void Unsubscribe(WindowContext window)
        {
            _windows.Remove(window.Id);
        }

        public void Handle(UpdateAuthorizationState update)
        {
            if (_isActive)
            {
                foreach (var window in _windows.Values)
                    window.Handle(update);
            }
            //else if (update.AuthorizationState is AuthorizationStateWaitPhoneNumber)
            //{
            //    TLContainer.Current.Lifecycle.Remove(this);
            //}
        }

        public void Handle(UpdateConnectionState update)
        {
            if (_isActive)
            {
                foreach (var window in _windows.Values)
                    window.Handle(update);
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
