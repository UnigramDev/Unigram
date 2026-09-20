//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Common
{
    /// <summary>
    /// Remembers what had focus before a popup took it, and hands it back on the way out.
    /// </summary>
    /// <remarks>
    /// Save once the popup's content has loaded, not when it is asked to open: a context menu item
    /// that opens one still has focus at that point and is destroyed with its flyout a moment
    /// later, so what gets saved could never be focused again.
    ///
    /// Restore before the popup closes, never after. ContentDialog::HideInternal puts it in the
    /// same order and says why: as the popup goes away the FocusManager moves focus to the first
    /// focusable element of the page, and anything done after that has already been undone.
    ///
    /// Weak, because the element can be gone by the time the popup closes - a page swapped, a
    /// container recycled. Focus is not always on a Control either: a Hyperlink is a TextElement,
    /// and ContentDialog carries its own special case for exactly that.
    /// </remarks>
    public struct FocusScope
    {
        private WeakReference _focused;

        public void Save(XamlRoot xamlRoot)
        {
            _focused = FocusManagerEx.TryGetFocusedElement(xamlRoot) is DependencyObject focused
                ? new WeakReference(focused)
                : null;
        }

        public void Restore()
        {
            var target = _focused?.Target;
            _focused = null;

            if (target is Control control)
            {
                control.Focus(FocusState.Programmatic);
            }
            else if (target is Hyperlink hyperlink)
            {
                hyperlink.Focus(FocusState.Programmatic);
            }
        }
    }
}
