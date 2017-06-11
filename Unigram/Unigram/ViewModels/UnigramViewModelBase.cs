using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Template10.Mvvm;

namespace Unigram.ViewModels
{
    /// <summary>
    /// Base class for ViewModel
    /// </summary>
    public class UnigramViewModelBase : ViewModelBase
    {
        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ITelegramEventAggregator _aggregator;

        public UnigramViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;
        }

        /// <summary>
        /// Gets a reference to the <see cref="Telegram.Api.Services.IMTProtoService"/> 
        /// class that handle API requests
        /// </summary>
        public IMTProtoService ProtoService
        {
            get
            {
                return _protoService;
            }
        }

        /// <summary>
        /// Gets a refernce to the <see cref="Telegram.Api.Services.Cache.ICacheService"/> 
        /// class that represente in memory cache
        /// </summary>
        public ICacheService CacheService
        {
            get
            {
                return _cacheService;
            }
        }

        public ITelegramEventAggregator Aggregator
        {
            get
            {
                return _aggregator;
            }
        }

        private bool _isLoading;
        public virtual bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                Set(ref _isLoading, value);
            }
        }
    }
}
