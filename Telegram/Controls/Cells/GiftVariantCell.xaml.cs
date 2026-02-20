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
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class GiftVariantCell : UserControl
    {
        public GiftVariantCell()
        {
            InitializeComponent();
        }

        public void UpdateModel(IClientService clientService, UpgradedGiftModel model)
        {
            Animated.Source = DelayedFileSource.FromSticker(clientService, model.Sticker);
            Pattern.Clear();

            Title.Text = model.Name;
            Title.RequestedTheme = ElementTheme.Default;

            Rarity.Text = model.Rarity.ToText();
            Rarity.ClearValue(BackgroundProperty);
        }

        public void UpdateBackdrop(IClientService clientService, UpgradedGiftModel model, UpgradedGiftBackdrop backdrop, UpgradedGiftSymbol symbol)
        {
            Animated.Source = DelayedFileSource.FromSticker(clientService, model.Sticker);
            Pattern.Update(clientService, backdrop, symbol);

            Title.Text = backdrop.Name;
            Title.RequestedTheme = ElementTheme.Dark;

            Rarity.Text = backdrop.Rarity.ToText();
            Rarity.Background = new SolidColorBrush(Color.FromArgb(0x54, 0, 0, 0));
        }

        public void UpdateSymbol(IClientService clientService, UpgradedGiftBackdrop backdrop, UpgradedGiftSymbol symbol)
        {
            Animated.Source = DelayedFileSource.FromSticker(clientService, symbol.Sticker);
            Pattern.Update(clientService, backdrop, symbol);

            Title.Text = symbol.Name;
            Title.RequestedTheme = ElementTheme.Dark;

            Rarity.Text = symbol.Rarity.ToText();
            Rarity.Background = new SolidColorBrush(Color.FromArgb(0x54, 0, 0, 0));
        }
    }
}
