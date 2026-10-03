//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation.Services;
using Windows.UI.Xaml.Controls;

namespace Telegram.Navigation
{
    /// <summary>
    /// A view that owns a navigation service. The window finds its services by walking these
    /// from its content, outermost first, instead of keeping a list of them.
    /// </summary>
    public interface INavigationHost
    {
        INavigationService NavigationService { get; }
    }

    /// <summary>
    /// Hosts a bare navigation frame as window content.
    /// </summary>
    public partial class NavigationHost : Grid, INavigationHost
    {
        public NavigationHost(INavigationService navigationService)
        {
            NavigationService = navigationService;
            Children.Add(navigationService.Frame);
        }

        public INavigationService NavigationService { get; }
    }
}
