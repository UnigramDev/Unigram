//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileLinksTabPage : ProfileTabPage
    {
        public ProfileLinksTabPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (IsProfile)
            {
                FindName(nameof(SearchRoot));
            }
            else
            {
                ScrollingHost.Style = BootStrapper.Current.Resources["DefaultListViewStyle"] as Style;
                ScrollingHost.Padding = new Thickness(0);
                ScrollingHost.ItemContainerCornerRadius = new CornerRadius(0);
            }

            if (ViewModel.Links.Empty())
            {
                ScrollingHost.ItemContainerTransitions.Add(new EntranceThemeTransition { IsStaggeringEnabled = false });
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || ViewModel == null)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedLinkCell cell && args.Item is MessageWithOwner message)
            {
                if (!IsProfile)
                {
                    args.ItemContainer.BorderThickness = new Thickness(0);
                    args.ItemContainer.Background = null;
                }

                cell.UpdateMessage(ViewModel.NavigationService, message);
                args.Handled = true;
            }
        }
    }
}
