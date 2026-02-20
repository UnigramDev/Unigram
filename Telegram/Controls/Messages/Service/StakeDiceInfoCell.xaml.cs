//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class StakeDiceInfoCell : Grid
    {
        public StakeDiceInfoCell(MessageViewModel message)
        {
            InitializeComponent();

            if (message.Content is not MessageStakeDice stakeDice)
            {
                return;
            }

            if (stakeDice.PrizeToncoinAmount != -1)
            {
                Text.Text = string.Format(Strings.StakeDiceActionYouWon, (stakeDice.PrizeToncoinAmount / Constants.ToncoinMin).ToString("0.#"));
            }
        }
    }
}
