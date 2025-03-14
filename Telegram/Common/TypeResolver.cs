//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.ViewModels.Delegates;
using Windows.Storage;

namespace Telegram.Views
{
    public partial class TypeResolver
    {
        private static readonly TypeResolver _instance = new TypeResolver();

        private readonly ConcurrentDictionary<int, TypeLocator> _containers = new ConcurrentDictionary<int, TypeLocator>();
        private readonly ILifetimeService _lifetime;
        private readonly IPasscodeService _passcode;
        private readonly ILocaleService _locale;
        private readonly IPlaybackService _playback;

        private TypeResolver()
        {
            _lifetime = new LifetimeService();
            _passcode = new PasscodeService(SettingsService.Current.PasscodeLock);
            _playback = new PlaybackService(SettingsService.Current);
            _locale = LocaleService.Current;
        }

        public int Count => _containers.Count;

        public void Configure()
        {
            var fail = true;
            var first = 0;

            foreach (var session in GetSessionsToInitialize())
            {
                if (first < 1 || session == SettingsService.Current.PreviousSession)
                {
                    first = session;
                }

                fail = false;
                Current.Build(session);
            }

            if (fail)
            {
                Current.Build(first);
            }

            _lifetime.Update();
        }

        private IEnumerable<int> GetSessionsToInitialize()
        {
            var folders = Directory.GetDirectories(ApplicationData.Current.LocalFolder.Path);

            var toBeDeleted = new HashSet<string>();
            var toBeInitialized = 0;

            foreach (var folder in folders)
            {
                if (int.TryParse(Path.GetFileName(folder), out int session))
                {
                    var container = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);
                    if (container.Values.ContainsKey("UserId"))
                    {
                        toBeInitialized++;
                        yield return session;
                    }
                    else
                    {
                        toBeDeleted.Add(folder);

                    }
                }
            }

            // We delete unauthorized sessions only if there's some active one.
            // This is just to remember proxy settings for the user in case they restart the app.
            if (toBeInitialized > 0 && toBeDeleted.Count > 0)
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
        }

        public ILifetimeService Lifetime => _lifetime;
        public IPasscodeService Passcode => _passcode;
        public ILocaleService Locale => _locale;
        public IPlaybackService Playback => _playback;

        public static TypeResolver Current
        {
            get
            {
                return _instance;
            }
        }

        public IEnumerable<ISessionService> GetSessions()
        {
            return ResolveAll<ISessionService>();
        }

        public IEnumerable<T> ResolveAll<T>()
        {
            foreach (var container in _containers.Values)
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

        public TypeLocator Build(int id)
        {
            return _containers[id] = new TypeLocator(_lifetime, _locale, _passcode, _playback, id, id == SettingsService.Current.ActiveSession);
        }

        public void Destroy(int id)
        {
            if (_containers.TryRemove(id, out _))
            {
                //container.Dispose();
            }
        }

        public TService Resolve<TService>()
        {
            return Resolve<TService>(int.MaxValue);
        }

        public TService Resolve<TService>(int session)
        {
            if (session == int.MaxValue)
            {
                session = _lifetime.ActiveItem?.Id ?? 0;
            }

            var result = default(TService);
            //if (_containers.TryGetValue(account, out IContainer container))
            if (_containers.TryGetValue(session, out TypeLocator container))
            {
                result = container.Resolve<TService>();
            }

            return result;
        }

        public bool TryResolve<TService>(int session, out TService result)
        {
            if (session == int.MaxValue)
            {
                session = _lifetime.ActiveItem?.Id ?? 0;
            }

            result = default;

            //if (_containers.TryGetValue(account, out IContainer container))
            if (_containers.TryGetValue(session, out TypeLocator container))
            {
                result = container.Resolve<TService>();
            }

            return result != null;
        }

        public TService Resolve<TService, TDelegate>(TDelegate delegato, int session)
            where TService : IDelegable<TDelegate>
            where TDelegate : IViewModelDelegate
        {
            if (session == int.MaxValue)
            {
                session = _lifetime.ActiveItem?.Id ?? 0;
            }

            var result = default(TService);
            //if (_containers.TryGetValue(account, out IContainer container))
            if (_containers.TryGetValue(session, out TypeLocator container))
            {
                result = container.Resolve<TService>();
            }

            if (result != null)
            {
                result.Delegate = delegato;
            }

            return result;
        }
    }
}
