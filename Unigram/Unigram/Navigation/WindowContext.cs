//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unigram.Common;
using Unigram.Navigation.Services;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.System;
using WinRT;
using WinRT.Interop;

namespace Unigram.Navigation
{
#warning TODO: heavy reactoring
    public partial class WindowContext
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
                //var mainDispatcher = CoreApplication.MainView.CoreWindow?.Dispatcher;
                //if (mainDispatcher == null)
                {
                    return null;
                }

                //return ActiveWrappers.FirstOrDefault(x => x.Window.Dispatcher == mainDispatcher) ??
                //        ActiveWrappers.FirstOrDefault();
            }
            //catch (COMException)
            catch
            {
                //MainView might exist but still be not accessible
                return ActiveWrappers.FirstOrDefault();
            }
        }

        public virtual void Initialize()
        {

        }








        class WindowsSystemDispatcherQueueHelper
        {
            [StructLayout(LayoutKind.Sequential)]
            struct DispatcherQueueOptions
            {
                internal int dwSize;
                internal int threadType;
                internal int apartmentType;
            }

            [DllImport("CoreMessaging.dll")]
            private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

            object m_dispatcherQueueController = null;
            public void EnsureWindowsSystemDispatcherQueueController()
            {
                if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
                {
                    // one already exists, so we'll just use it.
                    return;
                }

                if (m_dispatcherQueueController == null)
                {
                    DispatcherQueueOptions options;
                    options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                    options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                    options.apartmentType = 2; // DQTAT_COM_STA

                    CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
                }
            }
        }

        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        public bool TrySetMicaBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                Window.Activated += Window_Activated;
                Window.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_micaController.AddSystemBackdropTarget(this.Window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            Window.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }











        public bool IsInMainView { get; }

        public UIElement Content => Window.Content;

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

        [ThreadStatic]
        public static WindowContext Current;

        private static readonly Dictionary<XamlRoot, WindowContext> _mapping = new();

        /// <summary>
        /// Mirror of DisplayInformation.LogicalDpi / 96d
        /// Mimics XamlRoot.RasterizationScale
        /// </summary>
        public double RasterizationScale { get; private set; }

        public WindowContext(Window window)
        {
            if (Current != null)
            {
                throw new Exception("Windows already has a wrapper; use Current(window) to fetch.");
            }
            Current = this;
            Window = window;
            Dispatcher = new DispatcherContext(window.DispatcherQueue);
            IsInMainView = true; //CoreApplication.MainView == CoreApplication.GetCurrentView();
            ActiveWrappers.Add(this);

            //_mapping[window.Content.XamlRoot] = this;

            window.Closed += (s, e) =>
            {
                ActiveWrappers.Remove(this);
            };

            //window.DispatcherQueue.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            //window.CoreWindow.ResizeStarted += OnResizeStarted;
            //window.CoreWindow.ResizeCompleted += OnResizeCompleted;

            //var displayInformation = DisplayInformation.GetForCurrentView();
            //displayInformation.DpiChanged += OnDpiChanged;

            Size = new Size(window.Bounds.Width, window.Bounds.Height);
            RasterizationScale = 1d;
        }

        public static void AddWindow(WindowContext window)
        {
            _mapping[window.Window.Content.XamlRoot] = window;
        }

        public static WindowContext ForXamlRoot(XamlRoot xamlRoot)
        {
            _mapping.TryGetValue(xamlRoot, out var value);
            return value;
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            RasterizationScale = sender.LogicalDpi / 96d;
        }

        private void OnResizeStarted(Windows.UI.Core.CoreWindow sender, object args)
        {
            if (Window.Content is FrameworkElement element)
            {
                element.Width = sender.Bounds.Width;
                element.Height = sender.Bounds.Height;
                element.HorizontalAlignment = HorizontalAlignment.Left;
                element.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        private void OnResizeCompleted(Windows.UI.Core.CoreWindow sender, object args)
        {
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

        public DispatcherContext Dispatcher { get; }
        public NavigationServiceList NavigationServices { get; } = new NavigationServiceList();

        public event TypedEventHandler<Windows.UI.Core.CoreDispatcher, Windows.UI.Core.AcceleratorKeyEventArgs> AcceleratorKeyActivated;

        private void Dispatcher_AcceleratorKeyActivated(Windows.UI.Core.CoreDispatcher sender, Windows.UI.Core.AcceleratorKeyEventArgs args)
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

        public static bool IsKeyDown(VirtualKey key)
        {
            return InputKeyboardSource
                .GetKeyStateForCurrentThread(key)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        public static void Close(XamlRoot xamlRoot)
        {
            //                 await ApplicationView.GetForCurrentView().ConsolidateAsync();

            if (_mapping.TryGetValue(xamlRoot, out WindowContext window))
            {
                window.Close();
            }
        }



        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        private readonly HashSet<int> _screenCaptureDisabled = new();
        private bool _screenCaptureEnabled = true;

        public void DisableScreenCapture(int hash)
        {
            _screenCaptureDisabled.Add(hash);

            if (_screenCaptureDisabled.Count == 1 && _screenCaptureEnabled)
            {
                _screenCaptureEnabled = false;
                SetWindowDisplayAffinity(WindowNative.GetWindowHandle(Window), 0x00000001);
            }
        }

        public void EnableScreenCapture(int hash)
        {
            _screenCaptureDisabled.Remove(hash);

            if (_screenCaptureDisabled.Count == 0 && !_screenCaptureEnabled)
            {
                _screenCaptureEnabled = true;
                SetWindowDisplayAffinity(WindowNative.GetWindowHandle(Window), 0x00000000);
            }
        }
    }
}
