//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unigram.Logs;
using Unigram.Navigation;
using Windows.System;
using WinRT.Interop;

namespace Unigram.Services.Keyboard
{
    public class KeyboardListener
    {
        private readonly Window _window;
        public XamlRoot XamlRoot => _window.Content?.XamlRoot;

        public KeyboardListener(Window window)
        {
            _window = window;
        }

        public void Attach()
        {
            if (_window.Content == null)
            {
                return;
            }

            _window.Content.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
            _window.Content.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        public void Detach()
        {
            if (_window.Content == null)
            {
                return;
            }

            _window.Content.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
            _window.Content.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.GoBack
                or VirtualKey.NavigationLeft
                or VirtualKey.GamepadLeftShoulder
                or VirtualKey.Escape)
            {
                BootStrapper.Current.RaiseBackRequested(_window.Content.XamlRoot, e.Key);
            }
            else if (e.Key is VirtualKey.GoForward
                or VirtualKey.NavigationRight
                or VirtualKey.GamepadRightShoulder)
            {
                BootStrapper.Current.RaiseForwardRequested(_window.Content.XamlRoot);
            }
            else if (e.Key is VirtualKey.Back
                or VirtualKey.Left
                or VirtualKey.Right)
            {
                var alt = WindowContext.IsKeyDown(VirtualKey.Menu);
                var shift = WindowContext.IsKeyDown(VirtualKey.Shift);
                var control = WindowContext.IsKeyDown(VirtualKey.Control);
                var windows = WindowContext.IsKeyDown(VirtualKey.LeftWindows) || WindowContext.IsKeyDown(VirtualKey.RightWindows);

                if (alt && !shift && !control && !windows)
                {
                    if (e.Key is VirtualKey.Right)
                    {
                        BootStrapper.Current.RaiseForwardRequested(_window.Content.XamlRoot);
                    }
                    else
                    {
                        BootStrapper.Current.RaiseBackRequested(_window.Content.XamlRoot, e.Key);
                    }
                }
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(_window.Content);
            var properties = point.Properties;

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
                    BootStrapper.Current.RaiseBackRequested(_window.Content.XamlRoot);
                }

                if (forwardPressed)
                {
                    BootStrapper.Current.RaiseForwardRequested(_window.Content.XamlRoot);
                }
            }
        }
    }
}
