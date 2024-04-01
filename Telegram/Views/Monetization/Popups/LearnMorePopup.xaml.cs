//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Monetization.Popups
{
    public sealed partial class LearnMorePopup : ContentPopup
    {
        private readonly string _value;
        private readonly string _url;

        public LearnMorePopup()
        {
            InitializeComponent();

            //Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/CollectibleUsername.tgs");
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Icon.Play();
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
            MessageHelper.OpenUrl(null, null, _url);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
            MessageHelper.CopyText(_value);
        }
    }
}
