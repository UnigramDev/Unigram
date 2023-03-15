//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsWebSessionsPage : HostedPage
    {
        public SettingsWebSessionsViewModel ViewModel => DataContext as SettingsWebSessionsViewModel;

        public SettingsWebSessionsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.WebSessionsTitle;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Terminate(e.ClickedItem as ConnectedWebsite);
        }

        private void TerminateOthers_Click(object sender, RoutedEventArgs e)
        {
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is WebSessionCell cell)
            {
                cell.UpdateConnectedWebsite(ViewModel.ClientService, args.Item as ConnectedWebsite);
            }
        }
    }
}
