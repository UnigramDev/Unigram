//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
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
            ViewModel.TerminateCommand.Execute(e.ClickedItem);
        }

        private void TerminateOthers_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.TerminateOthersCommand.Execute(null);
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
