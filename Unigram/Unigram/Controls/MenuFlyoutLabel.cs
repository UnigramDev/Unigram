//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MenuFlyoutLabel : MenuFlyoutSeparator
    {
        public MenuFlyoutLabel()
        {
            DefaultStyleKey = typeof(MenuFlyoutLabel);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(MenuFlyoutLabel), new PropertyMetadata(null));

        #endregion

    }
}
