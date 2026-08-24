//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Host;
using Telegram.Navigation;
using Windows.System;
using Windows.UI.Input;

namespace Telegram.Services.Keyboard
{
    /// <summary>
    /// The Win32 half of <see cref="InputListener"/>. Its UWP twin listens to
    /// <c>CoreDispatcher.AcceleratorKeyActivated</c> and <c>CoreWindow.PointerPressed</c>; here
    /// the same two sources are the message loop, which is the only place that sees a key before
    /// the tree does. Routed events are not an alternative: they never fire for Alt-modified keys
    /// the way <c>SystemKeyDown</c> does, and they never fire at all while focus is in a WebView2,
    /// which is not part of the XAML tree.
    ///
    /// The decision logic below is deliberately a copy of the UWP half rather than a shared base:
    /// the two differ only in where the key comes from, and the UWP file ships to users, so it is
    /// left exactly as it is.
    /// </summary>
    public partial class InputListener : IMessageFilter
    {
        private readonly WindowContext _window;

        public InputListener(WindowContext window)
        {
            _window = window;
        }

        public void Release()
        {
            // Nothing to unsubscribe: the island drops its filter when the window closes.
        }

        bool IMessageFilter.PreTranslateMessage(ref MSG message)
        {
            if (message.message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
            {
                return OnAcceleratorKeyActivated((VirtualKey)message.wParam.ToInt32());
            }
            else if (message.message == Win32.WM_XBUTTONDOWN)
            {
                return OnPointerPressed(message.wParam.ToInt32() & 0xFFFF);
            }

            return false;
        }

        private bool OnAcceleratorKeyActivated(VirtualKey key)
        {
            if (key is VirtualKey.GoBack
                    or VirtualKey.NavigationLeft
                    or VirtualKey.GamepadLeftShoulder
                    or VirtualKey.Escape)
            {
                return _window.RaiseBackRequested(key);
            }
            else if (key is VirtualKey.GoForward
                         or VirtualKey.NavigationRight
                         or VirtualKey.GamepadRightShoulder)
            {
                return _window.RaiseForwardRequested();
            }
            else if (key is VirtualKey.Back
                         or VirtualKey.Left)
            {
                if (WindowContext.KeyModifiers() == VirtualKeyModifiers.Menu)
                {
                    return _window.RaiseBackRequested(key);
                }
            }
            else if (key is VirtualKey.Right)
            {
                if (WindowContext.KeyModifiers() == VirtualKeyModifiers.Menu)
                {
                    return _window.RaiseForwardRequested();
                }
            }
            else
            {
                var invoked = LifetimeService.Current.Shortcuts.Process(key, out VirtualKeyModifiers modifiers);
                if (invoked != null)
                {
                    return _window.RaiseShortcutInvoked(invoked, modifiers);
                }
            }

            return false;
        }

        /// <summary>
        /// Browser-style back and forward mouse buttons. <paramref name="buttons"/> is the low
        /// word of a mouse message wParam.
        /// </summary>
        private bool OnPointerPressed(int buttons)
        {
            // Ignore button chords with the left, right, and middle buttons
            if ((buttons & (Win32.MK_LBUTTON | Win32.MK_RBUTTON | Win32.MK_MBUTTON)) != 0)
            {
                return false;
            }

            // If back or forward are pressed (but not both) navigate appropriately
            bool backPressed = (buttons & Win32.MK_XBUTTON1) != 0;
            bool forwardPressed = (buttons & Win32.MK_XBUTTON2) != 0;
            if (backPressed ^ forwardPressed)
            {
                if (backPressed)
                {
                    _window.RaiseBackRequested();
                }
                else
                {
                    _window.RaiseForwardRequested();
                }

                return true;
            }

            return false;
        }

        public static bool IsPointerGoBackGesture(PointerPoint point)
        {
            var properties = point.Properties;

            // Ignore button chords with the left, right, and middle buttons
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed)
            {
                return false;
            }

            // If back or forward are pressed (but not both) navigate appropriately
            bool backPressed = properties.IsXButton1Pressed;
            return backPressed;
        }
    }
}
