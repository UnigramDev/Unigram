//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    /// <summary>
    /// A menu item with a second, smaller line under its text, saying what choosing it does.
    /// </summary>
    public partial class MenuFlyoutSubtitleItem : MenuFlyoutItem
    {
        public MenuFlyoutSubtitleItem()
        {
            DefaultStyleKey = typeof(MenuFlyoutSubtitleItem);
        }

        #region Subtitle

        public string Subtitle
        {
            get { return (string)GetValue(SubtitleProperty); }
            set { SetValue(SubtitleProperty, value); }
        }

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register("Subtitle", typeof(string), typeof(MenuFlyoutSubtitleItem), new PropertyMetadata(null));

        #endregion
    }
}
