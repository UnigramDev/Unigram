using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Storage;

namespace Unigram.Views
{
    public class TLContainer
    {
        private static readonly TLContainer _instance = new TLContainer();

        private readonly ConcurrentDictionary<int, TLLocator> _containers = new ConcurrentDictionary<int, TLLocator>();
        private readonly ILifetimeService _lifetime;
        private readonly IPasscodeService _passcode;
        private readonly ILocaleService _locale;

        private TLContainer()
        {
            _lifetime = new LifetimeService();
            _passcode = new PasscodeService(SettingsService.Current.PasscodeLock);
            _locale = LocaleService.Current;
        }

        public void Configure(out int count)
        {
            count = 0;

            var fail = true;
            var first = 0;

            foreach (var session in GetSessionsToInitialize())
            {
                if (first < 1 || session == SettingsService.Current.PreviousSession)
                {
                    first = session;
                }

                count++;
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

        public static TLContainer Current
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

        public TLLocator Build(int id)
        {
            return _containers[id] = new TLLocator(_lifetime, _locale, _passcode, id, id == SettingsService.Current.ActiveSession);
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
            if (_containers.TryGetValue(session, out TLLocator container))
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
            if (_containers.TryGetValue(session, out TLLocator container))
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
            if (_containers.TryGetValue(session, out TLLocator container))
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
