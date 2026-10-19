//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Wallet
{
    /// <summary>
    /// The frame round the selected sticker on the wallet card. It is drawn over the card's print
    /// rather than with the stickers, which sit under it, so its handles can always be reached.
    /// </summary>
    public sealed partial class WalletStickerFrame : UserControl
    {
        // Room between the sticker's edge and the ring.
        private const float Gap = 4;

        public WalletStickerFrame()
        {
            InitializeComponent();
        }

        public bool IsHandle(object source, out bool left)
        {
            for (var element = source as DependencyObject; element != null && element != this; element = VisualTreeHelper.GetParent(element))
            {
                if (element == LeftHandle || element == RightHandle)
                {
                    left = element == LeftHandle;
                    return true;
                }
            }

            left = false;
            return false;
        }

        /// <summary>
        /// Round a sticker of <paramref name="size"/>, centred on <paramref name="center"/> and
        /// turned by <paramref name="rotation"/> degrees, all in the card's face units.
        /// </summary>
        public void Show(Vector2 center, float size, float rotation)
        {
            var side = size + Gap * 2;

            Width = side;
            Height = side;

            Transform.CenterX = side / 2;
            Transform.CenterY = side / 2;
            Transform.TranslateX = center.X - side / 2;
            Transform.TranslateY = center.Y - side / 2;
            Transform.Rotation = rotation;

            Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }
    }
}
