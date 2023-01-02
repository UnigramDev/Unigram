//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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

        public TLMultipleViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
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
