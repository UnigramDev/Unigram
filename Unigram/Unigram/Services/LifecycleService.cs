using Autofac;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public interface ILifecycleService
    {
        void Update();

        ISessionService Create();
        ISessionService Remove(ISessionService item);
        ISessionService Remove(ISessionService item, ISessionService active);

        bool ShouldClose(ISessionService item);

        bool Contains(string phoneNumber);

        void Subscribe(WindowContext window);
        void Unsubscribe(WindowContext window);

        MvxObservableCollection<ISessionService> Items { get; }
        ISessionService ActiveItem { get; set; }
    }

    public class LifecycleService : ViewModelBase, ILifecycleService
    {
        public LifecycleService()
        {
            Items = new MvxObservableCollection<ISessionService>();
        }

        public void Update()
        {
            Items.ReplaceWith(TLContainer.Current.GetSessions());
            ActiveItem = Items.FirstOrDefault(x => x.IsActive) ?? Items.FirstOrDefault();
        }

        private void Update(ISessionService session)
        {
            Items.ReplaceWith(TLContainer.Current.GetSessions());
            ActiveItem = session;
        }

        public MvxObservableCollection<ISessionService> Items { get; }

        private ISessionService _previousItem;
        public ISessionService PreviousItem => _previousItem;

        private ISessionService _activeItem;
        public ISessionService ActiveItem
        {
            get
            {
                return _activeItem;
            }
            set
            {
                if (_activeItem != null)
                {
                    _activeItem.IsActive = false;
                    _previousItem = _activeItem;
                }

                if (value != null)
                {
                    value.IsActive = true;
                    ApplicationSettings.Current.SelectedAccount = value.Id;
                }

                Set(ref _activeItem, value);
            }
        }

        public ISessionService Create()
        {
            var app = App.Current as App;
            var id = Items.Max(x => x.Id) + 1;
            var container = app.Locator.Configure(id);

            var session = container.Resolve<ISessionService>();
            Update(session);

            return session;
        }

        public ISessionService Remove(ISessionService item)
        {
            return Remove(item, _previousItem ?? Create());
        }

        public ISessionService Remove(ISessionService item, ISessionService active)
        {
            Update(active);

            item.Aggregator.Unsubscribe(item);
            item.ProtoService.Send(new Close());

            return active;
        }

        public bool ShouldClose(ISessionService item)
        {
            return true;
        }

        public bool Contains(string phoneNumber)
        {
            foreach (var session in Items)
            {
                var user = session.ProtoService.GetUser(session.UserId);
                if (user == null)
                {
                    continue;
                }

                if (user.PhoneNumber.Contains(phoneNumber) || phoneNumber.Contains(user.PhoneNumber))
                {
                    return true;
                }
            }

            return false;
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
}
