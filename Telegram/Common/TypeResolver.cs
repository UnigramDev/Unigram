//
// Copyright Fela Ameghino 2015-2023
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
    public class TypeResolver
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
            foreach (var folder in folders)
            {
                if (int.TryParse(Path.GetFileName(folder), out int session))
                {
                    var container = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);
                    if (container.Values.ContainsKey("UserId"))
                    {
                        yield return session;
                    }
                    else
                    {
                        Task.Factory.StartNew((path) =>
                        {
                            try
                            {
                                Directory.Delete((string)path, true);
                            }
                            catch { }
                        }, folder);
                    }
                }
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

        public TService Resolve<TService>(int session = int.MaxValue)
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

        public TService Resolve<TService, TDelegate>(TDelegate delegato, int session = int.MaxValue)
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
