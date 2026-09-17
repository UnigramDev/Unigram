//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Cells.Revenue;
using Telegram.Td.Api;
using Telegram.ViewModels.Grams;
using Telegram.Views.Grams.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Grams
{
    public sealed partial class GramsPage : HostedPage
    {
        public GramsViewModel ViewModel => DataContext as GramsViewModel;

        public GramsPage()
        {
            InitializeComponent();
            Title = Strings.TONBalanceTitle;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TonTransaction transaction)
            {
                ViewModel.ShowPopup(new TonReceiptPopup(ViewModel.ClientService, transaction));
            }
        }

        private readonly ScrollPosition[] _positions = new ScrollPosition[3];
        private int _index;

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedIndex < 0 || Navigation.SelectedIndex == _index)
            {
                return;
            }

            // Each tab is meant to come back where it was left, and the offset it was left at
            // does not survive a tab whose content is shorter: the view clamps it on the way
            // through and there is nothing to clamp back to on the way in.
            _positions[_index] = ScrollingHost.SaveScrollPosition();

            _index = Navigation.SelectedIndex;
            ViewModel.SelectedIndex = _index;

            // The list keeps the same ItemsSource, so the panel holds its place by index through
            // the patch: a tab being opened for the first time would otherwise inherit the place
            // of the one it was switched from.
            var position = _positions[_index];
            if (position.IsEmpty)
            {
                return;
            }

            ScrollingHost.RestoreScrollPosition(position);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is TonTransactionCell cell && args.Item is TonTransaction info)
            {
                cell.UpdateInfo(ViewModel.ClientService, info);
                args.Handled = true;
            }
        }

        private void TopUp_Click(object sender, RoutedEventArgs e)
        {
            var url = ViewModel.ClientService.Options.GramTopUpUrl;
            MessageHelper.OpenUrl(null, null, string.IsNullOrEmpty(url) ? Strings.TopUpViaFragmentLink : url);
        }
    }
}
