//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Td.Api;

namespace Telegram.Views.Users.Popups
{
    public sealed partial class UserAffiliatePopup : ContentPopup
    {
        public UserAffiliatePopup(bool start, AffiliateProgramParameters parameters)
        {
            InitializeComponent();

            Title = Strings.AffiliateProgramAlert;

            PrimaryButtonText = start
                ? Strings.AffiliateProgramStartAlertButton
                : Strings.AffiliateProgramUpdateAlertButton;
            SecondaryButtonText = Strings.Cancel;

            MessageLabel.Text = start
                ? Strings.AffiliateProgramStartAlertText
                : Strings.AffiliateProgramUpdateAlertText;

            Commission.Content = parameters.CommissionPercent();
            Duration.Content = parameters.Duration();
        }
    }
}
