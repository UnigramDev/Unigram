//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Wallet
{
    // A gift dragged out of the list only ever lands on the card, but the list, inside the window's
    // drop target, treats itself as one too: it parts and animates the items under the pointer to show
    // where they would go. The drag events still bubble to the window, which is the real target; only
    // the list's own handling is skipped, here and in its items, which report to the list directly.
    public partial class WalletNftGridView : SelectGridView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new WalletNftGridViewItem();
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
        }

        protected override void OnDragOver(DragEventArgs e)
        {
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
        }

        protected override void OnDrop(DragEventArgs e)
        {
        }
    }

    public partial class WalletNftGridViewItem : GridViewItem
    {
        protected override void OnDragEnter(DragEventArgs e)
        {
        }

        protected override void OnDragOver(DragEventArgs e)
        {
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
        }

        protected override void OnDrop(DragEventArgs e)
        {
        }
    }
}
