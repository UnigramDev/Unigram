//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.System;

namespace Telegram.Services.Keyboard
{
    public partial class InputListener
    {
        private readonly Window _window;
        private UIElement _content;

        public InputListener(Window window)
        {
            _window = window;
            Update();
        }

        public void Update()
        {
            /*
             * InputKeyboardSource.GetForIsland(island)

            where you can obtain island via GetByVisual(visual)*/
            if (_content != null)
            {
                _content.ProcessKeyboardAccelerators -= OnProcessKeyboardAccelerators;
                _content.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
            }

            _content = _window.Content;

            if (_content != null)
            {
                _content.ProcessKeyboardAccelerators += OnProcessKeyboardAccelerators;
                _content.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            }
        }

        public void Release()
        {
            if (_content != null)
            {
                _content.ProcessKeyboardAccelerators -= OnProcessKeyboardAccelerators;
                _content.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs e)
        {
            if (/*e.EventType is not CoreAcceleratorKeyEventType.KeyDown and not CoreAcceleratorKeyEventType.SystemKeyDown ||*/
            e.Handled)
            {
                return;
            }

            var args = KeyboardEventArgs(e);
            if (args.VirtualKey is VirtualKey.GoBack
                                or VirtualKey.NavigationLeft
                                or VirtualKey.GamepadLeftShoulder
                                or VirtualKey.Escape)
            {
                BootStrapper.Current.RaiseBackRequested(sender.XamlRoot, args.VirtualKey);
            }
            else if (args.OnlyAlt && args.VirtualKey is VirtualKey.Back
                                                     or VirtualKey.Left)
            {
                BootStrapper.Current.RaiseBackRequested(sender.XamlRoot, args.VirtualKey);
            }
            else if (args.VirtualKey is VirtualKey.GoForward
                                     or VirtualKey.NavigationRight
                                     or VirtualKey.GamepadRightShoulder)
            {
                BootStrapper.Current.RaiseForwardRequested(sender.XamlRoot);
            }
            else if (args.OnlyAlt && args.VirtualKey is VirtualKey.Right)
            {
                BootStrapper.Current.RaiseForwardRequested(sender.XamlRoot);
            }
            else
            {
                try
                {
                    RaiseAsMulticastDelegate(args);
                }
                finally
                {
                    e.Handled = args.Handled;
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

        private InputKeyDownEventArgs KeyboardEventArgs(ProcessKeyboardAcceleratorEventArgs e)
        {
            return new InputKeyDownEventArgs
            {
                VirtualKey = e.Key,
                RepeatCount = 1,
                AltKey = (e.Modifiers & VirtualKeyModifiers.Menu) != 0,
                ControlKey = (e.Modifiers & VirtualKeyModifiers.Control) != 0,
                ShiftKey = (e.Modifiers & VirtualKeyModifiers.Shift) != 0
            };
        }

        /// <summary>
        /// Invoked on every mouse click, touch screen tap, or equivalent interaction when this
        /// page is active and occupies the entire window.  Used to detect browser-style next and
        /// previous mouse button clicks to navigate between pages.
        /// </summary>
        /// <param name="sender">Instance that triggered the event.</param>
        /// <param name="e">Event data describing the conditions that led to the event.</param>
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not UIElement element)
            {
                return;
            }

            var point = e.GetCurrentPoint(element);
            var properties = point.Properties;

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
                    BootStrapper.Current.RaiseBackRequested(element.XamlRoot);
                }

                if (forwardPressed)
                {
                    BootStrapper.Current.RaiseForwardRequested(element.XamlRoot);
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
