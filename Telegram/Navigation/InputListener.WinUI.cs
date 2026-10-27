//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Telegram.Navigation;
using Windows.System;

namespace Telegram.Services.Keyboard
{
    /// <summary>
    /// The WinUI 3 half of <see cref="InputListener"/>. Its UWP twin listens to
    /// <c>CoreDispatcher.AcceleratorKeyActivated</c> and its Win32 twin to the message loop; this
    /// one has neither, so both keys and pointer buttons come off the root as routed events with
    /// handledEventsToo.
    ///
    /// That is weaker than the other two and it is worth saying where: a routed KeyDown does not
    /// fire while focus is inside a WebView2, which is not part of the XAML tree, and a control
    /// that marks a key handled is only reached because of handledEventsToo. The framework's
    /// equivalent of the message filter is <c>InputPreTranslateKeyboardSource</c>, whose handler
    /// interface is not projected to C# - it takes a COM implementation - so it is not reached
    /// for here.
    ///
    /// The decision logic below is deliberately a copy of the other two halves rather than a
    /// shared base: they differ only in where the key comes from, and the UWP file ships to users,
    /// so it is left exactly as it is.
    /// </summary>
    public partial class InputListener
    {
        private readonly WindowContext _window;

        // Held in fields so RemoveHandler is given the same delegates AddHandler took, and because
        // a handler that cannot be removed leaks the window it belongs to.
        private readonly PointerEventHandler _pointerPressed;
        private readonly KeyEventHandler _keyDown;

        private UIElement _root;

        public InputListener(WindowContext window)
        {
            _window = window;

            _pointerPressed = OnPointerPressed;
            _keyDown = OnKeyDown;
        }

        /// <summary>
        /// Called from WindowContext.SetHostContent, the seam where the root arrives.
        /// </summary>
        internal void Attach(UIElement root)
        {
            Detach();

            _root = root;

            if (_root != null)
            {
                _root.AddHandler(UIElement.PointerPressedEvent, _pointerPressed, true);
                _root.AddHandler(UIElement.KeyDownEvent, _keyDown, true);
            }
        }

        private void Detach()
        {
            if (_root != null)
            {
                _root.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressed);
                _root.RemoveHandler(UIElement.KeyDownEvent, _keyDown);
                _root = null;
            }
        }

        public void Release()
        {
            Detach();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Only ever set, never cleared: handledEventsToo means this runs on keys something
            // else has already marked handled, and assigning the result would take that back.
            if (OnAcceleratorKeyActivated(e.Key))
            {
                e.Handled = true;
            }
        }

        private bool OnAcceleratorKeyActivated(VirtualKey key)
        {
            // See the UWP twin: while recording, every key belongs to the capture surface.
            if (LifetimeService.Current.Shortcuts.IsRecording)
            {
                return false;
            }

            // Ahead of navigation and the shortcuts, so a page that claims a key gets it before
            // Escape becomes a back request.
            if (_window.RaiseAcceleratorKeyActivated(key))
            {
                return true;
            }

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
        /// Browser-style back and forward mouse buttons. The tree handles neither XButton1 nor
        /// XButton2, so handledEventsToo still sees them.
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
