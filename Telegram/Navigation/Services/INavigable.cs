//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.Navigation.Services
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public interface INavigable
    {
        Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state);
        void NavigatedFrom(NavigationState suspensionState, bool suspending);
        void NavigatingFrom(NavigatingEventArgs args);
        INavigationService NavigationService { get; set; }
        IDispatcherContext Dispatcher { get; set; }
        IDictionary<string, object> SessionState { get; set; }
    }
}
