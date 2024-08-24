//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsWebSessionsPage : HostedPage
    {
        public SettingsWebSessionsViewModel ViewModel => DataContext as SettingsWebSessionsViewModel;

        public SettingsWebSessionsPage()
        {
            InitializeComponent();
            Title = Strings.WebSessionsTitle;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Terminate(e.ClickedItem as ConnectedWebsite);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is WebSessionCell cell)
            {
                cell.UpdateConnectedWebsite(ViewModel.ClientService, args.Item as ConnectedWebsite);
                args.Handled = true;
            }
        }
    }
}
