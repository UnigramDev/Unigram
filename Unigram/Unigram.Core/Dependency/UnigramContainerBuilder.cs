using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Dependency
{
    public class UnigramContainer
    {
        private static UnigramContainer _instance = new UnigramContainer();

        private ContainerBuilder _builder;
        private IContainer _container;
        private bool _isInitialized;

        private Dictionary<object, object> _cachedServices = new Dictionary<object, object>();

        private UnigramContainer() { }

        public static UnigramContainer Current
        {
            get
            {
                return _instance;
            }
        }

        public ContainerBuilder ContainerBuilder
        {
            get
            {
                return _builder;
            }
        }

        public void Reset()
        {
            _isInitialized = false;
            _container?.Dispose();
            _builder = new ContainerBuilder();
        }

        public void Build()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _container = _builder.Build();
            }
        }

        public TService ResolveType<TService>()
        {
            var result = default(TService);
            if (_container != null)
            {
                result = _container.Resolve<TService>();
            }

            return result;
        }

        public object ResolveType(Type type)
        {
            if (_container != null)
            {
                return _container.Resolve(type);
            }

            return null;
        }

        public TService ResolveType<TService>(object key)
        {
            if (_cachedServices.ContainsKey(key))
            {
                return (TService)_cachedServices[key];
            }

            var service = ResolveType<TService>();
            if (service != null)
            {
                _cachedServices[key] = service;
            }

            return service;
        }
    }
}
