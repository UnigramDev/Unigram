using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Services.NavigationService;
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

        public override IDispatcherWrapper Dispatcher
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
            await Task.WhenAll(Children.ToList().Select(x => x.OnNavigatedFromAsync(pageState, suspending)));
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            await Task.WhenAll(Children.ToList().Select(x => x.OnNavigatedToAsync(parameter, mode, state)));
        }

        public override async Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            await base.OnNavigatingFromAsync(args);
            await Task.WhenAll(Children.ToList().Select(x => x.OnNavigatingFromAsync(args)));
        }
    }

    public interface IChildViewModel
    {
        void Activate();
        void Deactivate();
    }
}
