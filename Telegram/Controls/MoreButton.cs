//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Assets.Icons;
using Windows.UI;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public class MoreButton : BadgeButton
    {
        public MoreButton()
        {
            DefaultStyleKey = typeof(MoreButton);
            IconSource = new More();

            ActualThemeChanged += OnActualThemeChanged;
            ThemeChanged();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ThemeChanged();
        }

        private void ThemeChanged()
        {
            if (IconSource != null)
            {
                IconSource.SetColorProperty("Foreground", ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White);
            }
        }
    }
}
