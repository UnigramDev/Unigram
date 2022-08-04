using System;
using System.Threading.Tasks;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class TLViewModelBase : ViewModelBase, INavigable
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public TLViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        public IProtoService ProtoService => _protoService;
        public ICacheService CacheService => _cacheService;

        public ISettingsService Settings => _settingsService;

        public IEventAggregator Aggregator => _aggregator;

        public int SessionId => _protoService.SessionId;

        public bool IsPremium => _protoService.IsPremium;
        public bool IsPremiumAvailable => _protoService.IsPremiumAvailable;


        private bool _isLoading;
        public virtual bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public virtual Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (this is IHandle)
            {
                //Subscribe();
            }

            return OnNavigatedToAsync(parameter, mode, state);
        }

        public virtual Task NavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            //if (this is IHandle)
            {
                Unsubscribe();
            }

            return OnNavigatedFromAsync(suspensionState, suspending);
        }

        public virtual void Subscribe()
        {

        }

        public void Unsubscribe()
        {
            Aggregator.Unsubscribe(this);
        }

        protected virtual void BeginOnUIThread(Windows.System.DispatcherQueueHandler action, Action fallback = null)
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
}
