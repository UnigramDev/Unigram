//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsSessionsPage : HostedPage
    {
        public SettingsSessionsViewModel ViewModel => DataContext as SettingsSessionsViewModel;

        public SettingsSessionsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.SessionsTitle;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.TerminateCommand.Execute(e.ClickedItem);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is SessionCell cell)
            {
                cell.UpdateSession(args.Item as Session);
            }

            // Table layout
            var first = false;
            var last = false;

            if (args.Item is Session session)
            {
                var list = session.IsPasswordPending ? ViewModel.Items.FirstOrDefault() : ViewModel.Items.LastOrDefault();
                if (list == null)
                {
                    return;
                }

                var index = list.IndexOf(session);
                first = index == 0;
                last = index == list.Count - 1;
            }

            var presenter = VisualTreeHelper.GetChild(args.ItemContainer, 0) as ListViewItemPresenter;
            if (presenter != null)
            {
                presenter.CornerRadius = new CornerRadius(first ? 8 : 0, first ? 8 : 0, last ? 8 : 0, last ? 8 : 0);
            }
        }
    }
}
