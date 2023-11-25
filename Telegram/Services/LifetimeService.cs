//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Views;
using Telegram.Views.Host;
using Windows.Storage;

namespace Telegram.Services
{
    public interface ILifetimeService
    {
        void Update();

        ISessionService Create(bool update = true, bool test = false);
        ISessionService Remove(ISessionService item);
        ISessionService Remove(ISessionService item, ISessionService active);

        void Destroy(ISessionService item);

        bool ShouldClose(ISessionService item);

        bool Contains(string phoneNumber);

        void Register(ISessionService item);
        void Unregister(ISessionService item);

        MvxObservableCollection<ISessionService> Items { get; }
        ISessionService ActiveItem { get; set; }
        ISessionService PreviousItem { get; set; }
    }

    public class LifetimeService : BindableBase, ILifetimeService
    {
        private readonly Dictionary<int, ISessionService> _sessions = new Dictionary<int, ISessionService>();

        public LifetimeService()
        {
            Items = new MvxObservableCollection<ISessionService>();
        }

        public void Register(ISessionService item)
        {
            _sessions[item.Id] = item;
        }

        public void Unregister(ISessionService item)
        {
            _sessions[item.Id] = null;
        }

        public void Update()
        {
            Items.ReplaceWith(TypeResolver.Current.GetSessions());
            ActiveItem = Items.FirstOrDefault(x => x.IsActive) ?? Items.FirstOrDefault();
        }

        private void Update(ISessionService session)
        {
            Items.ReplaceWith(TypeResolver.Current.GetSessions());
            ActiveItem = session;
        }

        public MvxObservableCollection<ISessionService> Items { get; }

        private ISessionService _previousItem;
        public ISessionService PreviousItem
        {
            get => _previousItem;
            set => _previousItem = value;
        }

        private ISessionService _activeItem;
        public ISessionService ActiveItem
        {
            get => _activeItem;
            set
            {
                if (_activeItem == value)
                {
                    return;
                }

                if (_activeItem != null)
                {
                    _activeItem.IsActive = false;
                    _previousItem = _activeItem;
                    SettingsService.Current.PreviousSession = _activeItem.Id;
                }

                if (value != null)
                {
                    value.IsActive = true;
                    SettingsService.Current.ActiveSession = value.Id;
                }

                //Set(ref _activeItem, value);
                _activeItem = value;
            }
        }

        public ISessionService Create(bool update = true, bool test = false)
        {
            var app = BootStrapper.Current as App;
            var sessions = TypeResolver.Current.GetSessions().ToList();
            var id = sessions.Count > 0 ? sessions.Max(x => x.Id) + 1 : 0;

            var settings = ApplicationData.Current.LocalSettings.CreateContainer($"{id}", ApplicationDataCreateDisposition.Always);
            settings.Values["UseTestDC"] = test;

            var container = TypeResolver.Current.Build(id);
            var session = container.Resolve<ISessionService>();
            if (update)
            {
                Update(session);
            }

            return session;
        }

        public ISessionService Remove(ISessionService item)
        {
            return Remove(item, _previousItem ?? Create());
        }

        public ISessionService Remove(ISessionService item, ISessionService active)
        {
            TypeResolver.Current.Destroy(item.Id);
            active ??= _previousItem ?? Create();
            Update(active);

            item.Aggregator.Unsubscribe(item);
            //WindowContext.Unsubscribe(item);

            WindowContext.Current.NavigationServices.RemoveByFrameId($"{item.Id}");
            WindowContext.Current.NavigationServices.RemoveByFrameId($"Main{item.Id}");

            return active;
        }

        public async void Destroy(ISessionService item)
        {
            ISessionService replace = null;
            if (item.IsActive)
            {
                ActiveItem = replace = _previousItem ?? Items.FirstOrDefault(x => x.Id != item.Id) ?? Create(false);
            }

            TypeResolver.Current.Destroy(item.Id);
            Update();

            item.Aggregator.Unsubscribe(item);
            //WindowContext.Unsubscribe(item);

            await WindowContext.ForEachAsync(async window =>
            {
                if (window.Content is RootPage root && replace != null)
                {
                    root.Switch(replace);
                }

                if (window.IsInMainView)
                {
                    window.NavigationServices.RemoveByFrameId($"{item.Id}");
                    window.NavigationServices.RemoveByFrameId($"Main{item.Id}");
                }
                else
                {
                    await WindowContext.Current.ConsolidateAsync();
                }
            });

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    Directory.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{item.Id}"), true);
                }
                catch { }
            });
        }

        public bool ShouldClose(ISessionService item)
        {
            return true;
        }

        public bool Contains(string phoneNumber)
        {
            foreach (var session in Items)
            {
                var user = session.ClientService.GetUser(session.UserId);
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
    }
}
