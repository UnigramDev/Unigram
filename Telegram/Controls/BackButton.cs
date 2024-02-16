//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class BackButton : GlyphButton
    {
        public BackButton()
        {
            DefaultStyleKey = typeof(BackButton);
            Click += OnClick;
        }

        private void OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var master = this.GetParent<MasterDetailView>();
            if (master != null)
            {
                if (master.NavigationService != null && master.NavigationService.CanGoBack)
                {
                    master.NavigationService.GoBack();
                    return;
                }
            }

            var page = this.GetParent<Page>();
            if (page != null)
            {
                if (page is INavigablePage navigable)
                {
                    var args = new BackRequestedRoutedEventArgs();
                    navigable.OnBackRequested(args);

                    if (args.Handled)
                    {
                        return;
                    }
                }

                if (page.DataContext is ViewModelBase viewModel && viewModel.NavigationService.CanGoBack)
                {
                    viewModel.NavigationService.GoBack();
                    return;
                }
                else if (page.Frame != null && page.Frame.CanGoBack)
                {
                    page.Frame.GoBack();
                    return;
                }
            }

            BootStrapper.Current.RaiseBackRequested();
        }
    }
}
