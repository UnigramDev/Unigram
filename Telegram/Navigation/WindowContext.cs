//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services.Keyboard;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-WindowWrapper
    public class WindowContext
    {
        public static WindowContext Default()
        {
            try
            {
                if (BootStrapper.Current.IsMainWindowCreated == false)
                {
                    return null;
                }

                //var mainDispatcher = CoreApplication.MainView.Dispatcher;
                var mainDispatcher = CoreApplication.MainView.CoreWindow?.Dispatcher;
                if (mainDispatcher == null)
                {
                    return null;
                }

                return ActiveWrappers.FirstOrDefault(x => x.Window.Dispatcher == mainDispatcher) ??
                        ActiveWrappers.FirstOrDefault();
            }
            //catch (COMException)
            catch
            {
                //MainView might exist but still be not accessible
                return ActiveWrappers.FirstOrDefault();
            }
        }

        public bool IsInMainView { get; }

        public UIElement Content
        {
            get => Window.Content;
            set
            {
                if (Window.Content?.XamlRoot != null)
                {
                    Window.Content.XamlRoot.Changed -= OnXamlRootChanged;
                }

                Window.Content = value;

                if (Window.Content?.XamlRoot != null)
                {
                    Window.Content.XamlRoot.Changed += OnXamlRootChanged;
                }
            }
        }

        public Size Size { get; set; }

        public ElementTheme ActualTheme => Window.Content is FrameworkElement element
            ? element.ActualTheme
            : ElementTheme.Default;

        public ElementTheme RequestedTheme
        {
            get => Window.Content is FrameworkElement element
                ? element.RequestedTheme
                : ElementTheme.Default;
            set
            {
                if (Window.Content is FrameworkElement element)
                {
                    element.RequestedTheme = value;
                }
            }
        }

        public static readonly List<WindowContext> ActiveWrappers = new List<WindowContext>();

        public static void ForEach(Action<TLWindowContext> action)
        {
            foreach (var window in ActiveWrappers.ToArray())
            {
                window.Dispatcher.Dispatch(() => action(window as TLWindowContext));
            }
        }

        public static Task ForEachAsync(Func<TLWindowContext, Task> action)
        {
            var tasks = new List<Task>();

            foreach (var window in ActiveWrappers.ToArray())
            {
                tasks.Add(window.Dispatcher.DispatchAsync(() => action(window as TLWindowContext)));
            }

            return Task.WhenAll(tasks);
        }

        [ThreadStatic]
        public static WindowContext Current;

        /// <summary>
        /// Mirror of DisplayInformation.LogicalDpi / 96d
        /// Mimics XamlRoot.RasterizationScale
        /// </summary>
        public double RasterizationScale { get; private set; }







        private readonly InputListener _inputListener;
        public InputListener InputListener => _inputListener;

        public WindowContext(Window window)
        {
            if (Current != null)
            {
                throw new Exception("Windows already has a wrapper; use Current(window) to fetch.");
            }
            Current = this;
            Window = window;
            Dispatcher = new DispatcherContext(window.CoreWindow.DispatcherQueue);
            IsInMainView = CoreApplication.MainView == CoreApplication.GetCurrentView();
            ActiveWrappers.Add(this);
            window.CoreWindow.Closed += (s, e) =>
            {
                ActiveWrappers.Remove(this);
            };
            window.Closed += (s, e) =>
            {
                ActiveWrappers.Remove(this);
            };

            _inputListener = new InputListener(window);

            window.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            window.CoreWindow.ResizeStarted += OnResizeStarted;
            window.CoreWindow.ResizeCompleted += OnResizeCompleted;

            Size = new Size(window.Bounds.Width, window.Bounds.Height);
            RasterizationScale = 1;
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            RasterizationScale = sender.RasterizationScale;
        }

        private void OnResizeStarted(CoreWindow sender, object args)
        {
            Logger.Debug();

            if (Window.Content is FrameworkElement element)
            {
                element.Width = sender.Bounds.Width;
                element.Height = sender.Bounds.Height;
                element.HorizontalAlignment = HorizontalAlignment.Left;
                element.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        private void OnResizeCompleted(CoreWindow sender, object args)
        {
            Logger.Debug();

            Size = new Size(sender.Bounds.Width, sender.Bounds.Height);

            if (Window.Content is FrameworkElement element)
            {
                element.Width = double.NaN;
                element.Height = double.NaN;
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        public void Close() { Window.Close(); }
        public Window Window { get; }

        public CoreWindow CoreWindow => Window.CoreWindow;
        public DispatcherContext Dispatcher { get; }
        public NavigationServiceList NavigationServices { get; } = new NavigationServiceList();

        public event TypedEventHandler<CoreDispatcher, AcceleratorKeyEventArgs> AcceleratorKeyActivated;

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (AcceleratorKeyActivated is MulticastDelegate multicast)
            {
                var list = multicast.GetInvocationList();
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    list[i].DynamicInvoke(sender, args);

                    if (args.Handled)
                    {
                        return;
                    }
                }
            }
        }



        private readonly HashSet<int> _screenCaptureDisabled = new();
        private bool _screenCaptureEnabled = true;

        public void DisableScreenCapture(int hash)
        {
            _screenCaptureDisabled.Add(hash);

            if (_screenCaptureDisabled.Count == 1 && _screenCaptureEnabled)
            {
                _screenCaptureEnabled = false;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = false;
            }
        }

        public void EnableScreenCapture(int hash)
        {
            _screenCaptureDisabled.Remove(hash);

            if (_screenCaptureDisabled.Count == 0 && !_screenCaptureEnabled)
            {
                _screenCaptureEnabled = true;
                ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = true;
            }
        }
    }
}
