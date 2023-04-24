//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsPasscodePopup : ContentPopup
    {
        public SettingsPasscodePopup()
        {
            this.InitializeComponent();

            Title = Strings.Passcode;
            PrimaryButtonText = Strings.Enable;
            SecondaryButtonText = Strings.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
