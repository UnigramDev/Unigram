using System;
using System.Threading;
#if WIN_RT
using Windows.UI.Core;
using Windows.UI.Popups;
using System.Threading.Tasks;
using Windows.System.Threading;
#else
using System.Windows;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.Foundation;

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
            Task.Run(async delegate
            {
                await Task.Delay(delay);
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
                }
            });
        }

        public static Task BeginOnThreadPoolAsync(Action action)
        {
            return Task.Run(() =>
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
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
                    TLUtils.WriteException(e);
                }
            });
        }

        public static IAsyncAction BeginOnUIThreadAsync(Action action)
        {
            return _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
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
                    TLUtils.WriteException(e);
                }
            });
        }

        public static void BeginOnUIThread(TimeSpan delay, Action action)
        {
            BeginOnThreadPool(delay, delegate
            {
                BeginOnUIThread(action);
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

        public static void OnUIThread(Action action)
        {
            if (_dispatcher == null)
            {
                return;
            }

            if (CheckAccess())
            {
                action.Invoke();
                return;
            }
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            BeginOnUIThread(() =>
            {
                action.Invoke();
                waitHandle.Set();
            });
            waitHandle.WaitOne();
        }

        public static void ShowMessageBox(string message)
        {
            new MessageDialog(message).ShowAsync();
        }

        public static void ShowDebugMessage(string message)
        {
        }
    }


    //    public static class Execute
    //    {
    //        public static void BeginOnThreadPool(TimeSpan delay, Action action)
    //        {
    //#if WIN_RT
    //            Task.Run(async () =>
    //            {
    //                await Task.Delay(delay);
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#else
    //            ThreadPool.QueueUserWorkItem(state =>
    //            {
    //                Thread.Sleep(delay);
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#endif
    //        }

    //        public static void BeginOnThreadPool(Action action)
    //        {
    //#if WIN_RT
    //            Task.Run(() =>
    //            {
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#else
    //            ThreadPool.QueueUserWorkItem(state =>
    //            {
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#endif
    //        }

    //        public static void BeginOnUIThread(Action action)
    //        {
    //#if WIN_RT
    //            var coreWindow = CoreWindow.GetForCurrentThread();
    //            if (coreWindow == null) return;

    //            var dispatcher = coreWindow.Dispatcher;
    //            if (dispatcher == null) return;

    //            dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
    //            {
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#else
    //            Deployment.Current.Dispatcher.BeginInvoke(() =>
    //            {
    //                try
    //                {
    //                    action?.Invoke();
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            });
    //#endif
    //        }

    //        public static void BeginOnUIThread(TimeSpan delay, Action action)
    //        {
    //            BeginOnThreadPool(delay, () =>
    //            {
    //                BeginOnUIThread(action);
    //            });
    //        }

    //        private static bool CheckAccess()
    //        {
    //#if WIN_RT
    //            var coreWindow = CoreWindow.GetForCurrentThread();
    //            if (coreWindow == null) return true;

    //            var dispatcher = coreWindow.Dispatcher;
    //            if (dispatcher == null) return true;

    //            return dispatcher.HasThreadAccess;
    //#else
    //            return Deployment.Current.Dispatcher.CheckAccess();
    //#endif
    //        }

    //        public static void OnUIThread(Action action)
    //        {
    //            if (CheckAccess())
    //            {
    //                action();
    //            }
    //            else
    //            {
    //                var waitHandle = new ManualResetEvent(false);
    //                BeginOnUIThread((() =>
    //                {
    //                    action();
    //                    waitHandle.Set();
    //                }));
    //                waitHandle.WaitOne();
    //            }
    //        }

    //        public static void ShowMessageBox(string message)
    //        {
    //#if SILVERLIGHT
    //            MessageBox.Show(message);
    //#elif WIN_RT
    //            new MessageDialog(message).ShowAsync();
    //#else
    //            Console.WriteLine(message);
    //#endif
    //        }

    //        public static void ShowDebugMessage(string message)
    //        {
    //#if DEBUG
    //            BeginOnUIThread(() => ShowMessageBox(message));
    //#endif
    //        }
    //    }
}
