//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.Storage;
using Windows.UI.Xaml.Media;

namespace Telegram.Services
{
    public interface ILifetimeService
    {
        ISession Create(bool update = true, bool test = false);
        void Destroy(ISession item);

        int Count { get; }

        IList<ISession> GetItemsForMenu(bool show, out long hash);

        IList<ISession> Items { get; }
        ISession ActiveItem { get; set; }
        ISession PreviousItem { get; set; }

        IEnumerable<T> ResolveAll<T>();
        bool TryResolve<T>(int session, out T result);
    }

    public partial class LifetimeService : ILifetimeService
    {
        private static readonly LifetimeService _instance = new();

        private readonly ReaderWriterDictionary<int, ISession> _sessions = new();
        private readonly IPasscodeService _passcode;
        private readonly ILocaleService _locale;
        private readonly IPlaybackService _playback;
        private readonly IShortcutsService _shortcuts;
        private readonly IProxyService _proxy;
        private readonly VoipCoordinator _voip;

        public IPasscodeService Passcode => _passcode;
        public ILocaleService Locale => _locale;
        public IPlaybackService Playback => _playback;
        public IShortcutsService Shortcuts => _shortcuts;
        public IProxyService Proxy => _proxy;
        public VoipCoordinator Voip => _voip;

        public LifetimeService()
        {
            _passcode = new PasscodeService(SettingsService.Current.PasscodeLock);
            _playback = new PlaybackService(SettingsService.Current);
            _shortcuts = new ShortcutsService();
            _proxy = new ProxyService(this);
            _voip = new VoipCoordinator();
            _locale = LocaleService.Current;

            var sessions = GetSessionsToInitialize(out int nextId)
                .OrderByDescending(s => s.IsActive)
                .ThenByDescending(s => s.IsPrevious)
                .ToList();

            for (int i = 0; i < sessions.Count; i++)
            {
                var available = sessions[i];
                var session = Build(available.Id, i == 0);

                _activeItem ??= session;
            }

            _activeItem ??= Build(nextId, true);
            _proxy.Migrate(_activeItem.Id);
        }

        public static void Initialize()
        {
            Logger.Info(Current.Count);
        }

        public static LifetimeService Current => _instance;

        public int Count => _sessions.Count;

        private ISession Build(int id, bool active)
        {
            var session = new SessionImpl(this, _locale, _passcode, _shortcuts, _proxy, id, active);
            _sessions[id] = session;
            return session;
        }

        record AvailableSession(int Id, bool IsActive, bool IsPrevious);

        private IList<AvailableSession> GetSessionsToInitialize(out int nextId)
        {
            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);

            var toBeDeleted = new HashSet<string>();
            var toBeInitialized = new List<AvailableSession>();

            var maxId = -1;

            foreach (var folder in folders)
            {
                if (int.TryParse(Path.GetFileName(folder), out int sessionId))
                {
                    maxId = Math.Max(maxId, sessionId);

                    var container = ApplicationData.Current.LocalSettings.CreateContainer($"{sessionId}", ApplicationDataCreateDisposition.Always);
                    if (container.Values.ContainsKey("UserId"))
                    {
                        toBeInitialized.Add(new AvailableSession(
                            sessionId,
                            sessionId == SettingsService.Current.ActiveSession,
                            sessionId == SettingsService.Current.PreviousSession));
                    }
                    else
                    {
                        toBeDeleted.Add(folder);
                    }
                }
            }

            // We delete unauthorized sessions only if there's some active one.
            // This is just to remember proxy settings for the user in case they restart the app.
            if (toBeInitialized.Count > 0 && toBeDeleted.Count > 0)
            {
                Task.Factory.StartNew(() =>
                {
                    foreach (var path in toBeDeleted)
                    {
                        try
                        {
                            Directory.Delete(path, true);
                        }
                        catch
                        {
                            // Directory or files might be locked
                        }
                    }
                });
            }

            if (toBeInitialized.Count == 0 && toBeDeleted.Count == 1)
            {
                nextId = Math.Max(0, maxId);
            }
            else
            {
                nextId = Math.Max(0, maxId + 1);
            }

            return toBeInitialized;
        }

        public IList<ISession> Items => _sessions.Values;

        public IList<ISession> GetItemsForMenu(bool show, out long hash)
        {
            IList<ISession> sessions = null;
            hash = 0;

            if (show)
            {
                foreach (var session in _sessions.OrderBy(x => { int index = Array.IndexOf(SettingsService.Current.AccountsSelectorOrder, x.Id); return index < 0 ? x.Id : index; }))
                {
                    hash = ((hash * 20261) + 0x80000000L + session.UserId) % 0x80000000L;

                    sessions ??= [];
                    sessions.Add(session);
                }
            }

            return sessions ?? Array.Empty<ISession>();
        }

        private ISession _previousItem;
        public ISession PreviousItem
        {
            get => _previousItem;
            set => _previousItem = value;
        }

        private ISession _activeItem;
        public ISession ActiveItem
        {
            get => _activeItem;
            set
            {
                if (_activeItem == value || !IsValidSession(value))
                {
                    return;
                }

                _activeItem.IsActive = false;
                _previousItem = _activeItem;
                SettingsService.Current.PreviousSession = _activeItem.Id;

                _activeItem = value;
                _activeItem.IsActive = true;
                SettingsService.Current.ActiveSession = value.Id;
            }
        }

        private bool IsValidSession(ISession session)
        {
            if (_sessions.TryGetValue(session.Id, out var active))
            {
                return active == session;
            }

            return false;
        }

        public ISession Create(bool update = true, bool test = false)
        {
            var app = BootStrapper.Current as App;
            var sessions = _sessions.Values;
            var id = sessions.Count > 0 ? sessions.Max(x => x.Id) + 1 : 0;

            var settings = ApplicationData.Current.LocalSettings.CreateContainer($"{id}", ApplicationDataCreateDisposition.Always);
            settings.Values["UseTestDC"] = test;

            var session = Build(id, update);

            if (update)
            {
                ActiveItem = session;
            }

            return session;
        }

        public async void Destroy(ISession item)
        {
            Logger.Info(item.Id);

            bool unlock = false;
            if (Count == 1)
            {
                _passcode.Reset();
                unlock = true;
            }

            ISession? replace = null;
            if (item.IsActive)
            {
                var previous = _previousItem == item ? null : _previousItem;
                var active = previous ?? Items.FirstOrDefault(x => x != item) ?? Create(false);

                ActiveItem = replace = active;
            }

            _sessions.Remove(item.Id);

            item.Aggregator.Unsubscribe(item);

            await WindowContext.ForEachAsync(window =>
            {
                if (window.IsInMainView)
                {
                    if (unlock)
                    {
                        window.Unlock();
                    }

                    if (window.Content is RootPage root && replace != null)
                    {
                        root.Switch(replace);
                    }

                    window.NavigationServices.RemoveByFrameId($"{item.Id}");
                    window.NavigationServices.RemoveByFrameId($"Main{item.Id}");

                    foreach (var popup in VisualTreeHelper.GetOpenPopups(window))
                    {
                        if (popup.Child is ContentPopup toast)
                        {
                            toast.Tag = null;
                            toast.Hide();
                        }
                    }

                    return Task.CompletedTask;
                }
                else
                {
                    return WindowContext.Current.ConsolidateAsync();
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

        public IEnumerable<T> ResolveAll<T>()
        {
            foreach (var container in _sessions)
            {
                if (container != null)
                {
                    var service = container.Resolve<T>();
                    if (service != null)
                    {
                        yield return service;
                    }
                }
            }
        }

        public bool TryResolve<T>(int session, out T result)
        {
            result = default;

            if (_sessions.TryGetValue(session, out ISession container))
            {
                result = container.Resolve<T>();
            }

            return result != null;
        }
    }
}
