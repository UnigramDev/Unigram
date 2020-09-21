using System;
using Unigram.Navigation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;

namespace Unigram.Services.Keyboard
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-KeyboardService
    public class KeyboardHelper
    {
        private readonly CoreWindow _window;
        private readonly WindowContext _context;
        public KeyboardHelper()
        {
            _context = WindowContext.GetForCurrentView();
            _context.AcceleratorKeyActivated += CoreDispatcher_AcceleratorKeyActivated;

            _window = Window.Current.CoreWindow;
            _window.PointerPressed += CoreWindow_PointerPressed;
        }

        public void Cleanup()
        {
            _window.Dispatcher.AcceleratorKeyActivated -= CoreDispatcher_AcceleratorKeyActivated;
            _window.PointerPressed -= CoreWindow_PointerPressed;
        }

        private void CoreDispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
        {
            if (e.EventType != CoreAcceleratorKeyEventType.KeyDown && e.EventType != CoreAcceleratorKeyEventType.SystemKeyDown || e.Handled)
            {
                return;
            }

            var args = KeyboardEventArgs(e.VirtualKey);
            args.EventArgs = e;

            try { KeyDown?.Invoke(args); }
            finally
            {
                e.Handled = e.Handled;
            }
        }

        public Action<KeyboardEventArgs> KeyDown { get; set; }

        private KeyboardEventArgs KeyboardEventArgs(VirtualKey key)
        {
            var alt = (_window.GetKeyState(VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            var shift = (_window.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            var control = (_window.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            var windows = ((_window.GetKeyState(VirtualKey.LeftWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
                || ((_window.GetKeyState(VirtualKey.RightWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down);
            return new KeyboardEventArgs
            {
                AltKey = alt,
                ControlKey = control,
                ShiftKey = shift,
                WindowsKey = windows,
                VirtualKey = key
            };
        }

        /// <summary>
        /// Invoked on every mouse click, touch screen tap, or equivalent interaction when this
        /// page is active and occupies the entire window.  Used to detect browser-style next and
        /// previous mouse button clicks to navigate between pages.
        /// </summary>
        /// <param name="sender">Instance that triggered the event.</param>
        /// <param name="e">Event data describing the conditions that led to the event.</param>
        private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs e)
        {
            var properties = e.CurrentPoint.Properties;

            // Ignore button chords with the left, right, and middle buttons
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed)
                return;

            // If back or foward are pressed (but not both) navigate appropriately
            bool backPressed = properties.IsXButton1Pressed;
            bool forwardPressed = properties.IsXButton2Pressed;
            if (backPressed ^ forwardPressed)
            {
                e.Handled = true;
                if (backPressed) RaisePointerGoBackGestured();
                if (forwardPressed) RaisePointerGoForwardGestured();
            }
        }

        public static bool IsPointerGoBackGesture(PointerPointProperties properties)
        {
            // Ignore button chords with the left, right, and middle buttons
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed)
                return false;

            // If back or foward are pressed (but not both) navigate appropriately
            bool backPressed = properties.IsXButton1Pressed;
            return backPressed;
        }

        public Action PointerGoForwardGestured { get; set; }
        protected void RaisePointerGoForwardGestured()
        {
            try { PointerGoForwardGestured?.Invoke(); }
            catch { }
        }

        public Action PointerGoBackGestured { get; set; }
        protected void RaisePointerGoBackGestured()
        {
            try { PointerGoBackGestured?.Invoke(); }
            catch { }
        }
    }
}
