//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    /// <summary>
    /// One row of wallet history: who, what, when, and how much. Filled from
    /// <c>ContainerContentChanging</c> like the other cells, so a recycled row costs a few property
    /// writes and no bindings.
    /// </summary>
    public sealed partial class WalletCollectibleCell : Grid
    {
        public WalletCollectibleCell()
        {
            InitializeComponent();
        }

        public void UpdateInfo(IClientService clientService, TonNft collectible)
        {
        }
    }
}
