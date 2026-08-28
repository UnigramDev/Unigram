//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.System;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public partial class ContentPopup
    {
        // ContentDialog marks every key but Escape and Enter handled, from a scope guard at the end
        // of its own accelerator handler on LayoutRoot, so that the dialog behaves like a modal. It
        // then sets an internal HandledShouldNotImpedeTextInput flag so the InputManager still
        // delivers the character to the focused text box.
        //
        // In a XAML island that flag does not hold: Tab, paste and KeyDown all work while typing
        // produces nothing at all. Measured in the spike - a ContentDialog types with any template
        // that does not give the framework a LayoutRoot part to attach that handler to, and stops
        // the moment it does, under ShowAsync and hand-hosted alike.
        //
        // This handler is on the same element and subscribed after the framework's, so it runs last
        // and can put Handled back. Only for unmodified keystrokes: a real accelerator still stays
        // handled and still cannot reach the window behind the dialog.
        partial void ReleaseTextInput(ProcessKeyboardAcceleratorEventArgs args)
        {
            if (args.Modifiers is VirtualKeyModifiers.None or VirtualKeyModifiers.Shift)
            {
                args.Handled = false;
            }
        }
    }
}
