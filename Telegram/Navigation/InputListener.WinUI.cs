//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Telegram.Navigation;
using Windows.System;

namespace Telegram.Services.Keyboard
{
    /// <summary>
    /// The WinUI 3 half of <see cref="InputListener"/>. Its UWP twin listens to
    /// <c>CoreDispatcher.AcceleratorKeyActivated</c>, which covers the whole window; this one
    /// listens to the keyboard and pointer sources of the window's content island, which is the
    /// nearest thing. Routed events on the root are not: popups live under the popup root, so a
    /// key or a click inside a ContentDialog or a ModalPopup never bubbles up to it.
    ///
    /// Where it still falls short: a WebView2 and a windowed popup - a menu let out of the window's
    /// bounds - each have an island of their own, so nothing typed into them arrives here. The
    /// framework's real equivalent of the message filter is <c>InputPreTranslateKeyboardSource</c>,
    /// whose handler interface is not projected to C#, and which takes a single handler.
    ///
    /// The decision logic below is deliberately a copy of the UWP half rather than a shared base:
    /// they differ only in where the key comes from, and the UWP file ships to users, so it is left
    /// exactly as it is.
    /// </summary>
    public partial class InputListener
    {
        private readonly WindowContext _window;

        private FrameworkElement _root;

        private InputKeyboardSource _keyboard;
        private InputPointerSource _pointer;

        public InputListener(WindowContext window)
        {
            _window = window;
        }

        /// <summary>
        /// Called from WindowContext.SetHostContent. The island is the window's for its whole
        /// life, but WinUI creates it after the content is first set, so until the root loads
        /// there is no XamlRoot to take it from.
        /// </summary>
        internal void Attach(UIElement content)
        {
            if (_keyboard != null || content is not FrameworkElement root)
            {
                return;
            }

            DetachRoot();

            if (root.XamlRoot != null)
            {
                Subscribe(root.XamlRoot);
            }
            else
            {
                _root = root;
                _root.Loaded += OnRootLoaded;
            }
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            var root = _root;
            DetachRoot();

            if (root.XamlRoot != null)
            {
                Subscribe(root.XamlRoot);
            }
        }

        private void Subscribe(XamlRoot xamlRoot)
        {
            var island = xamlRoot.ContentIsland;
            if (island == null)
            {
                return;
            }

            _keyboard = InputKeyboardSource.GetForIsland(island);
            _keyboard.KeyDown += OnKeyDown;
            _keyboard.SystemKeyDown += OnKeyDown;

            _pointer = InputPointerSource.GetForIsland(island);
            _pointer.PointerPressed += OnPointerPressed;
        }

        private void DetachRoot()
        {
            if (_root != null)
            {
                _root.Loaded -= OnRootLoaded;
                _root = null;
            }
        }

        public void Release()
        {
            DetachRoot();

            if (_keyboard != null)
            {
                _keyboard.KeyDown -= OnKeyDown;
                _keyboard.SystemKeyDown -= OnKeyDown;
                _keyboard = null;
            }

            if (_pointer != null)
            {
                _pointer.PointerPressed -= OnPointerPressed;
                _pointer = null;
            }
        }

        private void OnKeyDown(InputKeyboardSource sender, KeyEventArgs args)
        {
            // Only ever set, never cleared: the framework handles the island's keys too, and
            // assigning the result would take back a key it has already marked handled.
            if (OnAcceleratorKeyActivated(args.VirtualKey))
            {
                args.Handled = true;
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
        /// Browser-style back and forward mouse buttons.
        /// </summary>
        private void OnPointerPressed(InputPointerSource sender, PointerEventArgs args)
        {
            var properties = args.CurrentPoint.Properties;

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
                args.Handled = true;

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
