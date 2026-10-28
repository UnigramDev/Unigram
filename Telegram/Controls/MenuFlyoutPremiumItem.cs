//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    /// <summary>
    /// A menu item for a feature that needs Premium, marked with the star at its end. It still
    /// works: what is gated is sending the result.
    /// </summary>
    public partial class MenuFlyoutPremiumItem : MenuFlyoutItem
    {
        public MenuFlyoutPremiumItem()
        {
            DefaultStyleKey = typeof(MenuFlyoutPremiumItem);
        }
    }

    /// <inheritdoc cref="MenuFlyoutPremiumItem"/>
    public partial class ToggleMenuFlyoutPremiumItem : ToggleMenuFlyoutItem
    {
        public ToggleMenuFlyoutPremiumItem()
        {
            DefaultStyleKey = typeof(ToggleMenuFlyoutPremiumItem);
        }
    }
}
