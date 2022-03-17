using System;
using System.Collections.Generic;
using System.Linq;
using Unigram.Navigation.Services;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Navigation
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

        public UIElement Content => Window.Content;

        public static readonly List<WindowContext> ActiveWrappers = new List<WindowContext>();

        public static WindowContext GetForCurrentView() => ActiveWrappers.FirstOrDefault(x => x.Window == Window.Current) ?? Default();

        public static WindowContext Current(Window window) => ActiveWrappers.FirstOrDefault(x => x.Window == window);

        public WindowContext(Window window)
        {
            if (Current(window) != null)
            {
                throw new Exception("Windows already has a wrapper; use Current(window) to fetch.");
            }
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

            window.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        public void Close() { Window.Close(); }
        public Window Window { get; }
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



        private int _screenCaptureDisabled;

        public void SetScreenCaptureEnabled(bool enabled)
        {
            if (enabled)
            {
                _screenCaptureDisabled = Math.Max(_screenCaptureDisabled - 1, 0);
            }
            else
            {
                _screenCaptureDisabled++;
            }

            ApplicationView.GetForCurrentView().IsScreenCaptureEnabled = _screenCaptureDisabled == 0;
        }
    }
}
