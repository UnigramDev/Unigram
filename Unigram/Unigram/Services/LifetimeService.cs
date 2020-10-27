using Autofac;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Collections;
using Unigram.Navigation;
using Unigram.Views;
using Unigram.Views.Host;
using Windows.Storage;

namespace Unigram.Services
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

    public class LifetimeService : ViewModelBase, ILifetimeService
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
        public ISessionService PreviousItem
        {
            get { return _previousItem; }
            set { _previousItem = value; }
        }

        private ISessionService _activeItem;
        public ISessionService ActiveItem
        {
            get
            {
                return _activeItem;
            }
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
            var app = App.Current as App;
            var sessions = TLContainer.Current.GetSessions().ToList();
            var id = sessions.Count > 0 ? sessions.Max(x => x.Id) + 1 : 0;

            var settings = ApplicationData.Current.LocalSettings.CreateContainer($"{id}", ApplicationDataCreateDisposition.Always);
            settings.Values["UseTestDC"] = test;

            var container = app.Locator.Configure(id);
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
            TLContainer.Current.Destroy(item.Id);
            active = active ?? _previousItem ?? Create();
            Update(active);

            item.Aggregator.Unsubscribe(item);
            //WindowContext.Unsubscribe(item);

            WindowContext.GetForCurrentView().NavigationServices.RemoveByFrameId($"{item.Id}");
            WindowContext.GetForCurrentView().NavigationServices.RemoveByFrameId($"Main{item.Id}");

            return active;
        }

        public async void Destroy(ISessionService item)
        {
            ISessionService replace = null;
            if (item.IsActive)
            {
                ActiveItem = replace = _previousItem ?? Items.Where(x => x.Id != item.Id).FirstOrDefault() ?? Create(false);
            }

            TLContainer.Current.Destroy(item.Id);
            Update();

            item.Aggregator.Unsubscribe(item);
            //WindowContext.Unsubscribe(item);

            foreach (var window in WindowContext.ActiveWrappers.ToArray())
            {
                await window.Dispatcher.DispatchAsync(() =>
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
                        window.Close();
                    }
                });
            }

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
    }
}
