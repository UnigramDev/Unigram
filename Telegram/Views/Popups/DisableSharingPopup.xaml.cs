//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Services;

namespace Telegram.Views.Popups
{
    public sealed partial class DisableSharingPopup : ContentPopup
    {
        public DisableSharingPopup(IClientService clientService)
        {
            InitializeComponent();

            PrimaryButtonText = clientService.IsPremium ? Strings.DisableSharingInfoButton : Strings.PrivateMessagesChargePremiumLocked;
            ButtonsLayout = ContentPopupButtonsLayout.Vertical;
        }
    }
}
