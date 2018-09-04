using Autofac;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;

namespace Unigram.Views
{
    public class TLContainer
    {
        private static TLContainer _instance = new TLContainer();

        //private Dictionary<int, IContainer> _containers = new Dictionary<int, IContainer>();
        private ConcurrentDictionary<int, IContainer> _containers = new ConcurrentDictionary<int, IContainer>();
        private ILifecycleService _lifecycle;

        private TLContainer()
        {
            _lifecycle = new LifecycleService();
        }

        public ILifecycleService Lifecycle => _lifecycle;

        public static TLContainer Current
        {
            get
            {
                return _instance;
            }
        }

        public IEnumerable<ISessionService> GetSessions()
        {
            foreach (var container in _containers.Values)
            {
                if (container != null)
                {
                    yield return container.Resolve<ISessionService>();
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
            builder.RegisterInstance(_lifecycle).As<ILifecycleService>();

            return _containers[id] = factory(builder, id);
        }

        public TService Resolve<TService>(int account = int.MaxValue)
        {
            if (account == int.MaxValue)
            {
                account = _lifecycle.ActiveItem?.Id ?? 0;
            }

            var result = default(TService);
            //if (_containers.TryGetValue(account, out IContainer container))
            var container = _containers[account];
            {
                result = container.Resolve<TService>();
            }

            return result;
        }

        public TService Resolve<TService, TDelegate>(TDelegate delegato, int account = int.MaxValue)
            where TService : IDelegable<TDelegate>
            where TDelegate : IViewModelDelegate
        {
            if (account == int.MaxValue)
            {
                account = _lifecycle.ActiveItem?.Id ?? 0;
            }

            var result = default(TService);
            //if (_containers.TryGetValue(account, out IContainer container))
            var container = _containers[account];
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
