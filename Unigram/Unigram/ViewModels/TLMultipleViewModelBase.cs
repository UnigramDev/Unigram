using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class TLMultipleViewModelBase : TLViewModelBase
    {
        public readonly List<TLViewModelBase> Children;

        public TLMultipleViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Children = new List<TLViewModelBase>();
        }

        public override IDispatcherContext Dispatcher
        {
            get
            {
                return base.Dispatcher;
            }
            set
            {
                base.Dispatcher = value;

                foreach (var child in Children)
                {
                    child.Dispatcher = value;
                }
            }
        }

        public override INavigationService NavigationService
        {
            get
            {
                return base.NavigationService;
            }
            set
            {
                base.NavigationService = value;

                foreach (var child in Children)
                {
                    child.NavigationService = value;
                }
            }
        }

        public override IDictionary<string, object> SessionState
        {
            get
            {
                return base.SessionState;
            }
            set
            {
                base.SessionState = value;

                foreach (var child in Children)
                {
                    child.SessionState = value;
                }
            }
        }

        public override async Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            await base.OnNavigatedFromAsync(pageState, suspending);
            await Task.WhenAll(Children.Select(x => x.OnNavigatedFromAsync(pageState, suspending)));
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            await Task.WhenAll(Children.Select(x => x.OnNavigatedToAsync(parameter, mode, state)));
        }

        public override void OnNavigatingFrom(NavigatingEventArgs args)
        {
            base.OnNavigatingFrom(args);
            Children.ForEach(x => x.OnNavigatingFrom(args));
        }
    }

    public interface IChildViewModel
    {
        void Activate();
        void Deactivate();
    }
}
