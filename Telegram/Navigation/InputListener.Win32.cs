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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

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

        private readonly PointerEventHandler _pointerPressed;
        private UIElement _root;

        public InputListener(WindowContext window)
        {
            _window = window;

            // Held in a field so RemoveHandler is given the same delegate AddHandler took, and
            // because a handler that cannot be removed leaks the window it belongs to.
            _pointerPressed = OnPointerPressed;
        }

        /// <summary>
        /// Pointer input never reaches the message loop: an island feeds it through its own
        /// InputSite, so WM_XBUTTONDOWN is not in the queue to filter. It does reach XAML, which is
        /// why a routed handler works here where it would not for keys - the tree handles neither
        /// XButton1 nor XButton2, so handledEventsToo still sees them, and none of the reasons the
        /// keyboard needs the message loop (SystemKeyDown, a focused control swallowing the key,
        /// focus inside a WebView2) apply to a mouse button nothing consumes.
        ///
        /// Called from WindowContext.SetHostContent, the Win32 seam where the root arrives.
        /// </summary>
        internal void Attach(UIElement root)
        {
            Detach();

            _root = root;
            _root?.AddHandler(UIElement.PointerPressedEvent, _pointerPressed, true);
        }

        private void Detach()
        {
            _root?.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressed);
            _root = null;
        }

        public void Release()
        {
            // The island drops its filter when the window closes; the routed handler is ours.
            Detach();
        }

        bool IMessageFilter.PreTranslateMessage(ref MSG message)
        {
            if (message.message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
            {
                return OnAcceleratorKeyActivated((VirtualKey)message.wParam.ToInt32());
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
        /// Browser-style back and forward mouse buttons.
        /// </summary>
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(null).Properties;

            // Ignore button chords with the left, right, and middle buttons
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed)
            {
                return;
            }

            // If back or forward are pressed (but not both) navigate appropriately
            bool backPressed = properties.IsXButton1Pressed;
            bool forwardPressed = properties.IsXButton2Pressed;
            if (backPressed ^ forwardPressed)
            {
                e.Handled = true;

                if (backPressed)
                {
                    _window.RaiseBackRequested();
                }
                else
                {
                    _window.RaiseForwardRequested();
                }
            }
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
