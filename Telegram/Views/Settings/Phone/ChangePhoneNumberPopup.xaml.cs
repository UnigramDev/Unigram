//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class ChangePhoneNumberPopup : ContentPopup
    {
        public ChangePhoneNumberPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.OK;
        }
    }
}
