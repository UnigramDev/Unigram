using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Premium.Popups
{
    public sealed partial class UniqueStickersPopup : ContentPopup
    {
        public UniqueStickersPopup(IClientService clientService, Sticker sticker)
        {
            InitializeComponent();

            Presenter.UpdateFature(clientService, new[] { sticker });
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }
    }
}
