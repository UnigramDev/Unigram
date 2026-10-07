//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class WalletCollectibleCell : Grid
    {
        public WalletCollectibleCell()
        {
            InitializeComponent();
        }

        public void UpdateInfo(IClientService clientService, TonNft collectible)
        {
            if (collectible.Image != null)
            {
                Photo.Source = new ProfilePictureSourcePhoto(clientService, collectible.Image.Photo.Id, collectible.Image.Photo, null, Shape: ProfilePictureShape.Superellipse);
                Photo.Visibility = Visibility.Visible;
            }
            else
            {
                Photo.Source = null;
                Photo.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(collectible.Name))
            {
                Title.Text = Strings.WalletCollectible;
            }
            else
            {
                Title.Text = collectible.Name;
            }

            var model = collectible.Attributes.FirstOrDefault(x => string.Equals(x.TraitType, "Model", StringComparison.OrdinalIgnoreCase));
            var backdrop = collectible.Attributes.FirstOrDefault(x => string.Equals(x.TraitType, "Backdrop", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(model?.Value) || string.IsNullOrEmpty(backdrop?.Value))
            {
                Subtitle.Text = collectible.Description;
            }
            else
            {
                Subtitle.Text = string.Format(Strings.WalletCollectibleModelBackdrop, model.Value, backdrop.Value);
            }
        }
    }
}
