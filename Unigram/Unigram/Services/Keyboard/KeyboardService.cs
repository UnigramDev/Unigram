using System;
using System.Collections.Generic;
using Unigram.Logs;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Services.Keyboard
{
    public class KeyboardService
    {
        readonly KeyboardHelper _helper;

        private static readonly Dictionary<int, KeyboardService> _windowContext = new Dictionary<int, KeyboardService>();
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
                    Logger.Info("GoBack", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.NavigationLeft)
                {
                    Logger.Info("NavigationLeft", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadMenu)
                {
                    Logger.Info("GamepadMenu", member: nameof(AfterMenuGesture));
                    AfterMenuGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadLeftShoulder)
                {
                    Logger.Info("GamepadLeftShoulder", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Back)
                {
                    Logger.Info("Alt+Back", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Left)
                {
                    Logger.Info("Alt+Left", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.Escape)
                {
                    Logger.Info("Escape", member: nameof(AfterBackGesture));
                    AfterBackGesture?.Invoke(e.VirtualKey);
                }
                // use this to nav forward
                else if (e.VirtualKey == Windows.System.VirtualKey.GoForward)
                {
                    Logger.Info("GoForward", member: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.NavigationRight)
                {
                    Logger.Info("NavigationRight", member: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.VirtualKey == Windows.System.VirtualKey.GamepadRightShoulder)
                {
                    Logger.Info("GamepadRightShoulder", member: nameof(AfterForwardGesture));
                    AfterForwardGesture?.Invoke();
                }
                else if (e.OnlyAlt && e.VirtualKey == Windows.System.VirtualKey.Right)
                {
                    Logger.Info("Alt+Right", member: nameof(AfterForwardGesture));
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
                Logger.Info(member: nameof(KeyboardHelper.PointerGoBackGestured));
                AfterBackGesture?.Invoke(Windows.System.VirtualKey.GoBack);
            };
            _helper.PointerGoForwardGestured = () =>
            {
                Logger.Info(member: nameof(KeyboardHelper.PointerGoForwardGestured));
                AfterForwardGesture?.Invoke();
            };
        }

        public Action<Windows.System.VirtualKey> AfterBackGesture { get; set; }
        public Action AfterForwardGesture { get; set; }
        public Action AfterMenuGesture { get; set; }
    }

}
