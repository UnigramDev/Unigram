﻿using Autofac;
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

        private ContainerBuilder _builder = new ContainerBuilder();
        private IContainer _container;
        private bool _isInitialized;

        private Dictionary<object, object> _cachedServices = new Dictionary<object, object>();

        private UnigramContainer() { }

        public static UnigramContainer Instance
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
