//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class BurnedGiftCell : UserControl
    {
        public BurnedGiftCell()
        {
            InitializeComponent();
        }

        public void UpdateGift(IClientService clientService, ReceivedGift gift)
        {
            if (gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            Pattern.Update(clientService, upgraded.Gift);
            Pattern.Visibility = Visibility.Visible;

            Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

            RibbonRoot.Visibility = Visibility.Visible;
            Ribbon.Text = string.Format("#{0:N0}", upgraded.Gift.Number);

            RibbonTop.Color = _ribbonSoldOutTop;
            RibbonBottom.Color = _ribbonSoldOutBottom;
        }

        private readonly Color _ribbonSoldOutTop = Color.FromArgb(0xFF, 0xFF, 0x5B, 0x54);
        private readonly Color _ribbonSoldOutBottom = Color.FromArgb(0xFF, 0xED, 0x1D, 0x27);
    }
}
