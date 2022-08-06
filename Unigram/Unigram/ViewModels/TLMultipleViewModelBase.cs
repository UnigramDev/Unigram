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
            get => base.Dispatcher;
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
            get => base.NavigationService;
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
            get => base.SessionState;
            set
            {
                base.SessionState = value;

                foreach (var child in Children)
                {
                    child.SessionState = value;
                }
            }
        }

        public override async Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (this is IHandle)
            {
                Subscribe();
            }

            await OnNavigatedToAsync(parameter, mode, state);
            await Task.WhenAll(Children.Select(x => x.NavigatedToAsync(parameter, mode, state)));
        }

        public override async Task NavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            if (this is IHandle)
            {
                Unsubscribe();
            }

            await OnNavigatedFromAsync(suspensionState, suspending);
            await Task.WhenAll(Children.Select(x => x.NavigatedFromAsync(suspensionState, suspending)));
        }

        public override void NavigatingFrom(NavigatingEventArgs args)
        {
            base.NavigatingFrom(args);
            Children.ForEach(x => x.NavigatingFrom(args));
        }
    }

    public interface IChildViewModel
    {
        void Activate();
        void Deactivate();
    }
}
