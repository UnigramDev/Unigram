//
// Copyright Fela Ameghino 2015-2024
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
            InitializeComponent();

            PrimaryButtonText = Strings.Enable;
            SecondaryButtonText = Strings.Cancel;
        }
    }
}
