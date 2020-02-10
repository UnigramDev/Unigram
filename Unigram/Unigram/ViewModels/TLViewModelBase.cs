using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels
{
    /// <summary>
    /// Base class for ViewModel
    /// </summary>
    public class TLViewModelBase : ViewModelBase
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        private readonly IDispatcherWrapper _dispatcher;

        public TLViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        //public override IDispatcherWrapper Dispatcher
        //{
        //    get
        //    {
        //        return _dispatcher;
        //    }
        //    set
        //    {

        //    }
        //}

        public IProtoService ProtoService => _protoService;
        public ICacheService CacheService => _cacheService;

        public ISettingsService Settings => _settingsService;

        public IEventAggregator Aggregator => _aggregator;

        public int SessionId => _protoService.SessionId;

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

        protected virtual void BeginOnUIThread(Action action, Action fallback = null)
        {
            var dispatcher = Dispatcher;
            if (dispatcher == null)
            {
                dispatcher = WindowContext.Default()?.Dispatcher;
            }

            if (dispatcher != null)
            {
                dispatcher.Dispatch(action);
            }
            else if (fallback != null)
            {
                fallback();
            }
            else
            {
                try
                {
                    action();
                }
                catch { }
            }
        }
    }

#if INCLUDE_WALLET
    public class TonViewModelBase : TLViewModelBase
    {
        private readonly ITonService _tonService;

        public TonViewModelBase(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _tonService = tonService;
        }

        public ITonService TonService => _tonService;
    }
#endif
}
