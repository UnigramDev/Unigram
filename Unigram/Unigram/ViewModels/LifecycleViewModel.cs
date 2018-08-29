using Autofac;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.Storage;

namespace Unigram.ViewModels
{
    public class LifecycleViewModel : ViewModelBase
    {
        public LifecycleViewModel()
        {
            Items = new MvxObservableCollection<SessionViewModel>();
        }

        public void Update()
        {
            Items.ReplaceWith(TLContainer.Current.GetSessions());
            SelectedItem = Items.FirstOrDefault(x => x.IsSelected);
        }

        private void Update(SessionViewModel session)
        {
            Items.ReplaceWith(TLContainer.Current.GetSessions());
            SelectedItem = session;
        }

        public MvxObservableCollection<SessionViewModel> Items { get; }

        private SessionViewModel _previousItem;
        public SessionViewModel PreviousItem => _previousItem;

        private SessionViewModel _selectedItem;
        public SessionViewModel SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (_selectedItem != null)
                {
                    _selectedItem.IsSelected = false;
                    _previousItem = _selectedItem;
                }

                if (value != null)
                {
                    value.IsSelected = true;
                }

                Set(ref _selectedItem, value);
            }
        }

        public SessionViewModel CreateNewSession()
        {
            var app = App.Current as App;
            var id = Items.Max(x => x.Id) + 1;
            var container = app.Locator.Configure(id);

            var session = container.Resolve<SessionViewModel>();
            Update(session);

            return session;
        }

        public SessionViewModel RemoveSession(SessionViewModel session)
        {
            var replace = _previousItem ?? CreateNewSession();
            Update(replace);

            session.Aggregator.Unsubscribe(session);
            session.ProtoService.Send(new Close());

            return replace;
        }

        public void Subscribe(WindowContext window)
        {
            foreach (var item in Items)
            {
                item.Subscribe(window);
            }
        }

        public void Unsubscribe(WindowContext window)
        {
            foreach (var item in Items)
            {
                item.Unsubscribe(window);
            }
        }
    }

    public class SessionViewModel : TLViewModelBase, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateAuthorizationState>, IHandle<UpdateConnectionState>
    {
        private readonly Dictionary<long, WindowContext> _windows = new Dictionary<long, WindowContext>();

        public SessionViewModel(bool selected, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            WindowContext.Subscribe(this);
            aggregator.Subscribe(this);

            IsSelected = selected;
            UnreadCount = ProtoService.UnreadCount;
        }

        public int UserId => ProtoService.GetMyId();
        public int Id => ProtoService.SessionId;

        private int _unreadCount;
        public int UnreadCount
        {
            get
            {
                return _unreadCount;
            }
            set
            {
                Set(ref _unreadCount, value);
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                Set(ref _isSelected, value);
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
            if (_isSelected)
            {
                foreach (var window in _windows.Values)
                    window.Handle(update);
            }
            else if (update.AuthorizationState is AuthorizationStateWaitPhoneNumber)
            {
                TLContainer.Current.Lifecycle.RemoveSession(this);
            }
        }

        public void Handle(UpdateConnectionState update)
        {
            if (_isSelected)
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
