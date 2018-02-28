using System;
using System.Threading;
using Windows.UI.Core;
using Windows.UI.Popups;
using System.Threading.Tasks;
using Windows.System.Threading;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.Foundation;
using System.Diagnostics;

namespace Telegram.Api.Helpers
{
    public static class Execute
    {
        private static CoreDispatcher _dispatcher;
        public static CoreDispatcher Dispatcher
        {
            get
            {
                return _dispatcher;
            }
        }

        public static void Initialize()
        {
            _dispatcher = Window.Current.Dispatcher;
        }

        public static void BeginOnThreadPool(TimeSpan delay, Action action)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {

                }
            });
        }

        public static void BeginOnThreadPool(Action action)
        {
            Task.Run(() =>
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {

                }
            });
        }

        public static void BeginOnUIThread(Action action)
        {
            if (_dispatcher == null)
            {
                //try
                //{
                //    action?.Invoke();
                //}
                //catch (Exception e)
                //{
                //    TLUtils.WriteException(e);
                //}

                return;
            }

            //CoreWindow forCurrentThread = CoreWindow.GetForCurrentThread();
            //if (forCurrentThread == null)
            //{
            //    return;
            //}
            //CoreDispatcher dispatcher = forCurrentThread.Dispatcher;
            //if (dispatcher == null)
            //{
            //    return;
            //}
            //CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(0, delegate




            //CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(0, delegate
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {

                }
            });
        }

        private static bool CheckAccess()
        {
            if (_dispatcher == null)
            {
                return false;
            }

            //return CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess;
            return _dispatcher.HasThreadAccess;

            //CoreWindow forCurrentThread = CoreWindow.GetForCurrentThread();
            //if (forCurrentThread == null)
            //{
            //    return true;
            //}
            //CoreDispatcher dispatcher = forCurrentThread.Dispatcher;
            //return dispatcher == null || dispatcher.HasThreadAccess;
        }

        [Conditional("DEBUG")]
        public static void ShowDebugMessage(string message)
        {
        }
    }
}
