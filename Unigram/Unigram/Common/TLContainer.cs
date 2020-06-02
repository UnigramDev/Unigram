using Autofac;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unigram.Services;
using Unigram.ViewModels.Delegates;

namespace Unigram.Views
{
    public class TLContainer
    {
        private static TLContainer _instance = new TLContainer();

        //private Dictionary<int, IContainer> _containers = new Dictionary<int, IContainer>();
        private ConcurrentDictionary<int, IContainer> _containers = new ConcurrentDictionary<int, IContainer>();
        private ILifetimeService _lifetime;
        private IPasscodeService _passcode;
        private ILocaleService _locale;

        private TLContainer()
        {
            _lifetime = new LifetimeService();
            _passcode = new PasscodeService(SettingsService.Current.PasscodeLock);
            _locale = LocaleService.Current;
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
                    yield return container.Resolve<T>();
                }
            }
        }

        public IContainer Build(int id, Func<ContainerBuilder, int, IContainer> factory)
        {
            //for (int i = 0; i < Telegram.Api.Constants.AccountsMaxCount; i++)
            //{
            //    //if (_containers.ContainsKey(i))
            //    if (_containers[i] != null)
            //    {
            //        continue;
            //    }

            //}

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_lifetime).As<ILifetimeService>();
            builder.RegisterInstance(_passcode).As<IPasscodeService>();
            builder.RegisterInstance(_locale).As<ILocaleService>();

            return _containers[id] = factory(builder, id);
        }

        public void Destroy(int id)
        {
            if (_containers.TryRemove(id, out IContainer container))
            {
                container.Dispose();
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
            if (_containers.TryGetValue(session, out IContainer container))
            {
                result = container.Resolve<TService>();
            }

            return result;
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
            if (_containers.TryGetValue(session, out IContainer container))
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
