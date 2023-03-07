//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;

namespace Unigram.Views.Settings.Popups
{
    public sealed partial class SettingsPasscodePopup : ContentPopup
    {
        public SettingsPasscodePopup()
        {
            this.InitializeComponent();

            Title = Strings.Resources.Passcode;
            PrimaryButtonText = Strings.Resources.Enable;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
