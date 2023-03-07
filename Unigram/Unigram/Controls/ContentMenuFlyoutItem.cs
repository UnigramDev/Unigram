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
    public class ContentMenuFlyoutItem : MenuFlyoutItem
    {
        public ContentMenuFlyoutItem()
        {
            DefaultStyleKey = typeof(ContentMenuFlyoutItem);
        }

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(ContentMenuFlyoutItem), new PropertyMetadata(null));
    }
}
