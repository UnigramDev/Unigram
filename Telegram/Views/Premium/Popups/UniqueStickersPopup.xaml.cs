//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class UniqueStickersPopup : ContentPopup
    {
        public UniqueStickersPopup(IClientService clientService, Sticker sticker)
        {
            InitializeComponent();

            Presenter.UpdateFeature(clientService, new[] { sticker });
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Purchase_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {

        }

        private void PurchaseShadow_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {

        }
    }
}
