//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Chats;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public class LazoListViewItem : ListViewItem
    {
        private readonly ChatListView _parent;

        public LazoListViewItem(ChatListView parent)
        {
            _parent = parent;
        }

        public new void PointerPressed(PointerRoutedEventArgs e)
        {
            if (!CantSelect())
            {
                _parent.OnPointerPressed(this, e);
            }
        }

        public new void PointerEntered(PointerRoutedEventArgs e)
        {
            _parent.OnPointerEntered(this, e);
        }

        public new void PointerMoved(PointerRoutedEventArgs e)
        {
            _parent.OnPointerMoved(this, e);
        }

        public new void PointerReleased(PointerRoutedEventArgs e)
        {
            _parent.OnPointerReleased(this, e);
        }

        public new void PointerCanceled(PointerRoutedEventArgs e)
        {
            _parent.OnPointerCanceled(this, e);
        }

        public virtual bool CantSelect()
        {
            return true;
        }
    }
}
