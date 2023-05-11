//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;

namespace Telegram.Services.Keyboard
{
    public class InputListener
    {
        private readonly Window _window;

        public InputListener(Window window)
        {
            _window = window;

            _window.Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
            _window.CoreWindow.PointerPressed += OnPointerPressed;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
        {
            if (e.EventType is not CoreAcceleratorKeyEventType.KeyDown and not CoreAcceleratorKeyEventType.SystemKeyDown || e.Handled)
            {
                return;
            }

            var args = KeyboardEventArgs(e);
            if (args.VirtualKey is VirtualKey.GoBack
                                or VirtualKey.NavigationLeft
                                or VirtualKey.GamepadLeftShoulder
                                or VirtualKey.Escape)
            {
                BootStrapper.Current.RaiseBackRequested(args.VirtualKey);
            }
            else if (args.OnlyAlt && args.VirtualKey is VirtualKey.Back
                                                     or VirtualKey.Left)
            {
                BootStrapper.Current.RaiseBackRequested(args.VirtualKey);
            }
            else if (args.VirtualKey is VirtualKey.GoForward
                                     or VirtualKey.NavigationRight
                                     or VirtualKey.GamepadRightShoulder)
            {
                BootStrapper.Current.RaiseForwardRequested();
            }
            else if (args.OnlyAlt && args.VirtualKey is VirtualKey.Right)
            {
                BootStrapper.Current.RaiseForwardRequested();
            }
            else
            {
                try
                {
                    RaiseAsMulticastDelegate(args);
                }
                finally
                {
                    e.Handled = e.Handled;
                }
            }
        }

        private void RaiseAsMulticastDelegate(InputKeyDownEventArgs args)
        {
            if (KeyDown is MulticastDelegate multicast)
            {
                var list = multicast.GetInvocationList();
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    list[i].DynamicInvoke(_window, args);

                    if (args.Handled)
                    {
                        return;
                    }
                }
            }
        }

        public event TypedEventHandler<Window, InputKeyDownEventArgs> KeyDown;

        private InputKeyDownEventArgs KeyboardEventArgs(AcceleratorKeyEventArgs e)
        {
            return new InputKeyDownEventArgs
            {
                EventArgs = e,
                VirtualKey = e.VirtualKey,
                AltKey = WindowContext.IsKeyDown(VirtualKey.Menu),
                ControlKey = WindowContext.IsKeyDown(VirtualKey.Control),
                ShiftKey = WindowContext.IsKeyDown(VirtualKey.Shift),
                WindowsKey = WindowContext.IsKeyDown(VirtualKey.LeftWindows)
                    || WindowContext.IsKeyDown(VirtualKey.RightWindows),
            };
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

            // If back or foward are pressed (but not both) navigate appropriately
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

        public static bool IsPointerGoBackGesture(PointerPointProperties properties)
        {
            // Ignore button chords with the left, right, and middle buttons
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed)
            {
                return false;
            }

            // If back or foward are pressed (but not both) navigate appropriately
            bool backPressed = properties.IsXButton1Pressed;
            return backPressed;
        }
    }
}
