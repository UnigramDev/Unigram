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

namespace Telegram.Api.Helpers
{
    public static class Execute
    {
        public static void BeginOnThreadPool(TimeSpan delay, Action action)
        {
#if WIN_RT
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#else
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(delay);
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#endif
        }

        public static void BeginOnThreadPool(Action action)
        {
#if WIN_RT
            Task.Run(() =>
            {
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#else
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#endif
        }

        public static void BeginOnUIThread(Action action)
        {
#if WIN_RT
            var coreWindow = CoreWindow.GetForCurrentThread();
            if (coreWindow == null) return;

            var dispatcher = coreWindow.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#else
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    action.SafeInvoke();
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                }
            });
#endif
        }

        public static void BeginOnUIThread(TimeSpan delay, Action action)
        {
            BeginOnThreadPool(delay, () =>
            {
                BeginOnUIThread(action);
            });
        }

        private static bool CheckAccess()
        {
#if WIN_RT
            var coreWindow = CoreWindow.GetForCurrentThread();
            if (coreWindow == null) return true;

            var dispatcher = coreWindow.Dispatcher;
            if (dispatcher == null) return true;

            return dispatcher.HasThreadAccess;
#else
            return Deployment.Current.Dispatcher.CheckAccess();
#endif
        }

        public static void OnUIThread(Action action)
        {
            if (CheckAccess())
            {
                action();
            }
            else
            {
                var waitHandle = new ManualResetEvent(false);
                BeginOnUIThread((() =>
                {
                    action();
                    waitHandle.Set();
                }));
                waitHandle.WaitOne();
            }
        }

        public static void ShowMessageBox(string message)
        {
#if SILVERLIGHT
            MessageBox.Show(message);
#elif WIN_RT
            new MessageDialog(message).ShowAsync();
#else
            Console.WriteLine(message);
#endif
        }

        public static void ShowDebugMessage(string message)
        {
#if DEBUG
            BeginOnUIThread(() => ShowMessageBox(message));
#endif
        }
    }
}
