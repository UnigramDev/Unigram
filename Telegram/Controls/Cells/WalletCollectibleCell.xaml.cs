//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Cells
{
    /// <summary>
    /// An NFT in the wallet. One that is a Telegram gift shows the gift, and can be put on the card.
    /// </summary>
    public sealed partial class WalletCollectibleCell : UserControl
    {
        // A third of the collectibles grid at its widest.
        private const int PlainImageSize = 120;

        private IClientService _clientService;
        private TonNft _collectible;

        // The gift whose outline is drawn, or being asked for.
        private UpgradedGift _outlineGift;

        public WalletCollectibleCell()
        {
            InitializeComponent();
        }

        public TonNft Collectible => _collectible;

        // The art is collapsed while the gift is out of the cell, and the outline takes its place.
        public double ArtSize => Animated.Visibility == Visibility.Visible
            ? Animated.ActualWidth
            : OutlineRoot.ActualWidth;

        public void UpdateInfo(IClientService clientService, TonNft collectible, bool outOfCell, bool onCard)
        {
            _clientService = clientService;
            _collectible = collectible;

            var gift = collectible.Gift;
            if (gift == null)
            {
                UpdatePlain(clientService, collectible);
                return;
            }

            if (PlainRoot != null)
            {
                PlainRoot.Visibility = Visibility.Collapsed;
            }

            Pattern.Visibility = Visibility.Visible;
            Pattern.Update(clientService, gift);

            Animated.Source = new DelayedFileSource(clientService, gift.Model.Sticker);

            RibbonTop.Color = gift.Backdrop.Colors.CenterColor.ToColor().Darken();
            RibbonBottom.Color = gift.Backdrop.Colors.EdgeColor.ToColor().Darken();
            Ribbon.Visibility = Visibility.Visible;

            UpdateOnCard(outOfCell, onCard);

            AutomationProperties.SetName(this, gift.ToName());
        }

        /// <summary>
        /// Out of the cell, on the card or on its way, the gift leaves its outline behind in the
        /// colour of the backdrop's symbols, as the place it goes back to. The art is collapsed
        /// meanwhile rather than recoloured, so nothing plays under the outline. The ribbon says
        /// it is on the card only once it has been put there.
        /// </summary>
        public void UpdateOnCard(bool outOfCell, bool onCard)
        {
            if (_collectible?.Gift is not UpgradedGift gift)
            {
                return;
            }

            Animated.Visibility = outOfCell ? Visibility.Collapsed : Visibility.Visible;
            OutlineRoot.Visibility = outOfCell ? Visibility.Visible : Visibility.Collapsed;

            if (outOfCell)
            {
                ShowOutline(gift);
            }

            Ribbon.Text = onCard ? Strings.WalletCardOnCard : string.Format("#{0:N0}", gift.Number);
        }

        private async void ShowOutline(UpgradedGift gift)
        {
            if (_outlineGift == gift)
            {
                return;
            }

            _outlineGift = gift;
            ElementCompositionPreview.SetElementChildVisual(OutlineRoot, null);

            var sticker = gift.Model.Sticker;

            var response = await _clientService.SendAsync(new GetStickerOutlineSvgPath(sticker.StickerValue.Id, false, false));
            if (response is not Text path || path.TextValue.Length == 0 || _outlineGift != gift)
            {
                return;
            }

            var compositor = ElementComposition.GetElementVisual(OutlineRoot).Compositor;

            var shape = compositor.CreateSpriteShape(compositor.CreatePathGeometry(new CompositionPath(CompositionPathParser.Parse(null, path.TextValue))));
            shape.FillBrush = compositor.CreateColorBrush(gift.Backdrop.Colors.SymbolColor.ToColor());

            // The path is in the sticker's own units, fitted as the art would be.
            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.ViewBox = compositor.CreateViewBox();
            visual.ViewBox.Size = new Vector2(sticker.Width, sticker.Height);
            visual.ViewBox.Stretch = CompositionStretch.Uniform;

            ElementCompositionPreview.SetElementChildVisual(OutlineRoot, visual);
        }

        private void UpdatePlain(IClientService clientService, TonNft collectible)
        {
            Pattern.Visibility = Visibility.Collapsed;
            Animated.Visibility = Visibility.Collapsed;
            Animated.Source = null;
            OutlineRoot.Visibility = Visibility.Collapsed;
            Ribbon.Visibility = Visibility.Collapsed;

            FindName(nameof(PlainRoot));
            PlainRoot.Visibility = Visibility.Visible;

            // Decoded along its shorter side, the one UniformToFill fits to the cell.
            var image = collectible.Image;
            var portrait = image != null && image.Width < image.Height;
            Photo.SetSource(clientService, image?.Photo, portrait ? PlainImageSize : 0, portrait ? 0 : PlainImageSize);

            var name = string.IsNullOrEmpty(collectible.Name)
                ? Strings.WalletCollectible
                : collectible.Name;

            Title.Text = name;
            Title.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;

            AutomationProperties.SetName(this, name);
        }
    }
}
