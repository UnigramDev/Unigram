using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Services.Keyboard
{
    public class KeyboardService
    {
        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Services.Logging.Severities severity = Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Logging.LoggingService.WriteLine(text, severity, caller: $"{nameof(KeyboardService)}.{caller}");

        #endregion

        KeyboardHelper _helper;

        private static Dictionary<int, KeyboardService> _windowContext = new Dictionary<int, KeyboardService>();
        public static KeyboardService GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out KeyboardService value))
            {
                return value;
            }

            var context = new KeyboardService();
            _windowContext[id] = context;

            return context;
        }

        private KeyboardService()
        {
            _helper = new KeyboardHelper();
            _helper.KeyDown = e =>
            {
                e.Handled = true;

                // use this to nav back
                if (e.VirtualKey == Windows.System.VirtualKey.GoBack)
                {
                    DebugWrite("GoBack", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.NavigationLeft)
                {
                    DebugWrite("NavigationLeft", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadMenu)
                {
                    DebugWrite("GamepadMenu", caller: nameof(AfterMenuGesture));
                    AfterMenuGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadLeftShoulder)
                {
                    DebugWrite("GamepadLeftShoulder", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Back)
                {
                    DebugWrite("Alt+Back", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Left)
                {
                    DebugWrite("Alt+Left", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.Escape)
                {
                    DebugWrite("Escape", caller: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                // use this to nav forward
                else if (e.VirtualKey == Windows.System.VirtualKey.GoForward)
                {
                    DebugWrite("GoForward", caller: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.NavigationRight)
                {
                    DebugWrite("NavigationRight", caller: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadRightShoulder)
                {
                    DebugWrite("GamepadRightShoulder", caller: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Right)
                {
                    DebugWrite("Alt+Right", caller: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }

                // anything else
                else
                {
                    e.Handled = false;
                }
            };
            _helper.PointerGoBackGestured = () =>
            {
                DebugWrite(caller: nameof(KeyboardHelper.PointerGoBackGestured));
                AfterBackGesture?.Invoke(Windows.System.VirtualKey.GoBack);
            };
            _helper.PointerGoForwardGestured = () =>
            {
                DebugWrite(caller: nameof(KeyboardHelper.PointerGoForwardGestured));
                AfterForwardGesture?.Invoke();
            };
        }

        public Action<Windows.System.VirtualKey> AfterBackGesture { get; set; }
        public Action AfterForwardGesture { get; set; }
        public Action AfterMenuGesture { get; set; }
    }

}
