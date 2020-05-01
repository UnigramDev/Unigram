﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Core;
using Unigram.Services.Navigation;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using System.Diagnostics;
using Windows.UI.Core;
using Windows.Foundation.Metadata;
using Windows.Foundation;

namespace Unigram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-WindowWrapper
    public class WindowContext
    {
        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Services.Logging.Severities severity = Services.Logging.Severities.Template10, [CallerMemberName]string caller = null) =>
            Services.Logging.LoggingService.WriteLine(text, severity, caller: $"WindowWrapper.{caller}");

        #endregion

        static WindowContext()
        {
            DebugWrite(caller: "Static Constructor");
        }

        public static WindowContext Default()
        {
            try
            {
                if (BootStrapper.Current.IsMainWindowCreated == false)
                {
                    return null;
                }

                //var mainDispatcher = CoreApplication.MainView.Dispatcher;
                var mainDispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
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

        public object Content => Dispatcher.Dispatch(() => Window.Content);

        public readonly static List<WindowContext> ActiveWrappers = new List<WindowContext>();

        public static WindowContext GetForCurrentView() => ActiveWrappers.FirstOrDefault(x => x.Window == Window.Current) ?? Default();

        public static WindowContext Current(Window window) => ActiveWrappers.FirstOrDefault(x => x.Window == window);

        public static WindowContext Current(INavigationService nav) => ActiveWrappers.FirstOrDefault(x => x.NavigationServices.Contains(nav));

        public DisplayInformation DisplayInformation() => Dispatcher.Dispatch(() => Windows.Graphics.Display.DisplayInformation.GetForCurrentView());

        public ApplicationView ApplicationView() => Dispatcher.Dispatch(() => Windows.UI.ViewManagement.ApplicationView.GetForCurrentView());

        public UIViewSettings UIViewSettings() => Dispatcher.Dispatch(() => Windows.UI.ViewManagement.UIViewSettings.GetForCurrentView());

        public WindowContext(Window window)
        {
            if (Current(window) != null)
            {
                throw new Exception("Windows already has a wrapper; use Current(window) to fetch.");
            }
            Window = window;
            ActiveWrappers.Add(this);
            Dispatcher = new DispatcherWrapper(window.Dispatcher);
            IsInMainView = CoreApplication.MainView == CoreApplication.GetCurrentView();
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
        public DispatcherWrapper Dispatcher { get; }
        public NavigationServiceList NavigationServices { get; } = new NavigationServiceList();

        public event TypedEventHandler<CoreDispatcher, AcceleratorKeyEventArgs> AcceleratorKeyActivated;

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (AcceleratorKeyActivated is MulticastDelegate multicast)
            {
                var list = multicast.GetInvocationList();
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    var result = list[i].DynamicInvoke(sender, args);
                    if (args.Handled)
                    {
                        return;
                    }
                }
            }
        }
    }
}
