//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;

namespace Telegram.Services.Keyboard
{
    public partial class InputListener
    {
        private readonly Window _window;

        public InputListener(Window window)
        {
            _window = window;

            _window.Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
            _window.CoreWindow.PointerPressed += OnPointerPressed;
        }

        public void Release()
        {
            _window.Dispatcher.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;
            _window.CoreWindow.PointerPressed -= OnPointerPressed;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType is not CoreAcceleratorKeyEventType.KeyDown and not CoreAcceleratorKeyEventType.SystemKeyDown || args.Handled)
            {
                return;
            }

            if (args.VirtualKey is VirtualKey.GoBack
                                or VirtualKey.NavigationLeft
                                or VirtualKey.GamepadLeftShoulder
                                or VirtualKey.Escape)
            {
                args.Handled = BootStrapper.Current.RaiseBackRequested(null, args.VirtualKey);
            }
            else if (args.VirtualKey is VirtualKey.GoForward
                                     or VirtualKey.NavigationRight
                                     or VirtualKey.GamepadRightShoulder)
            {
                args.Handled = BootStrapper.Current.RaiseForwardRequested();
            }
            else if (args.VirtualKey is VirtualKey.Back
                                     or VirtualKey.Left)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == VirtualKeyModifiers.Menu)
                {
                    args.Handled = BootStrapper.Current.RaiseBackRequested(null, args.VirtualKey);
                }
            }
            else if (args.VirtualKey is VirtualKey.Right)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == VirtualKeyModifiers.Menu)
                {
                    args.Handled = BootStrapper.Current.RaiseForwardRequested();
                }
            }
            else
            {
                var invoked = LifetimeService.Current.Shortcuts.Process(args, out VirtualKeyModifiers modifiers);
                if (invoked != null)
                {
                    args.Handled = BootStrapper.Current.RaiseShortcutInvoked(invoked, modifiers);
                }
            }
        }

        /// <summary>
        /// Invoked on every mouse click, touch screen tap, or equivalent interaction when this
        /// page is active and occupies the entire window.  Used to detect browser-style next and
        /// previous mouse button clicks to navigate between pages.
        /// </summary>
        /// <param name="sender">Instance that triggered the event.</param>
        /// <param name="e">Event data describing the conditions that led to the event.</param>
        private void OnPointerPressed(CoreWindow sender, PointerEventArgs e)
        {
            var properties = e.CurrentPoint.Properties;

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
                    BootStrapper.Current.RaiseBackRequested();
                }

                if (forwardPressed)
                {
                    BootStrapper.Current.RaiseForwardRequested();
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
