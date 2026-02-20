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
    public sealed partial class SuggestedActionSetBirthdateCard : HyperlinkButton
    {
        public SuggestedActionSetBirthdateCard()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler HideClick
        {
            add => Hide.Click += value;
            remove => Hide.Click -= value;
        }
    }
}
