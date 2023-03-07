//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Navigation.Services;

namespace Unigram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-MVVM
    public abstract class ViewModelBase : BindableBase
    {
        protected virtual Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            return Task.CompletedTask;
        }

        public virtual void NavigatingFrom(NavigatingEventArgs args)
        {

        }

        public virtual INavigationService NavigationService { get; set; }

        public virtual IDispatcherContext Dispatcher { get; set; }

        public virtual IDictionary<string, object> SessionState { get; set; }

        public XamlRoot XamlRoot => NavigationService.XamlRoot;
    }
}