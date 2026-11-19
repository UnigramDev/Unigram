//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Stars.Popups
{
    /// <summary>
    /// How the price of an upgrade drops over time, from the schedule the upgrade preview carries.
    /// </summary>
    public sealed partial class GiftUpgradePricesPopup : ModalPopup
    {
        public GiftUpgradePricesPopup(GiftUpgradePreview preview)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Understood;

            var now = DateTime.Now.ToUnixTimeSeconds();
            var prices = preview.Prices;
            var added = false;

            // Only what is still to come, starting from the price in effect now. Once the whole
            // schedule is in the past it is shown as it was, rather than as nothing.
            for (int i = 0; i < prices.Count; i++)
            {
                if (now > prices[i].Date && (i + 1 >= prices.Count || now > prices[i + 1].Date))
                {
                    continue;
                }

                Table.Items.Add(CreateRow(prices[i]));
                added = true;
            }

            if (!added)
            {
                foreach (var price in prices)
                {
                    Table.Items.Add(CreateRow(price));
                }
            }
        }

        private static TableViewItem CreateRow(GiftUpgradePrice price)
        {
            // Table sets the font that carries the star glyph.
            var text = new TextBlock
            {
                Text = Icons.PremiumStar + " " + price.StarCount.ToString("N0"),
                Margin = new Thickness(8, 5, 8, 6),
                VerticalAlignment = VerticalAlignment.Top
            };

            return new TableViewItem
            {
                Header = Formatter.DateAt(price.Date),
                Content = text,
                Padding = new Thickness(0)
            };
        }
    }
}
