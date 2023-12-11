using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Telegram.Navigation;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;

namespace Telegram.Common
{
    public class NotifyIcon
    {
        private static AppServiceConnection _connection;
        private static BackgroundTaskDeferral _deferral;

        private static TaskCompletionSource<bool> _pendingExit;

        public static async Task LaunchAsync()
        {
            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                try
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                }
                catch
                {
                    // The app has been compiled without desktop bridge
                }
            }
        }

        public static bool IsConnected => _connection != null;

        public static async void Connect(AppServiceConnection connection, BackgroundTaskDeferral deferral)
        {
            _connection = connection;
            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            _deferral = deferral;

            var values = new ValueSet
            {
                { "ProcessId", Process.GetCurrentProcess().Id },
                { "OpenText", Strings.NotifyIconOpen },
                { "ExitText", Strings.NotifyIconExit }
            };

            if (_pendingExit != null)
            {
                values.Add("Exit", true);
            }

            await _connection.SendMessageAsync(values);

            _pendingExit?.TrySetResult(true);
            _pendingExit = null;
        }

        public static Task EnqueueExitAsync()
        {
            _pendingExit = new TaskCompletionSource<bool>();
            Cancel();

            return _pendingExit.Task;
        }

        public static async Task ExitAsync()
        {
            if (_connection != null)
            {
                try
                {
                    var task = _connection.SendMessageAsync("Exit").AsTask();
                    var result = await Task.WhenAny(task, Task.Delay(200));

                    if (task != result)
                    {
                        await EnqueueExitAsync();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static async Task DebugAsync(string value)
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.SendMessageAsync("Debug", value);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static void LoopbackExempt(bool enabled)
        {
            if (_connection != null)
            {
                try
                {
                    _ = _connection.SendMessageAsync("LoopbackExempt", enabled);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static void SendUnreadCount(int unreadCount, int unreadMutedCount)
        {
            if (_connection != null)
            {
                try
                {
                    _ = _connection.SendMessageAsync(new ValueSet { { "UnreadCount", unreadCount }, { "UnreadUnmutedCount", unreadMutedCount } });
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        private static async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.ContainsKey("Exit"))
            {
                await BootStrapper.ConsolidateAsync();
            }
        }

        private static void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            _connection.RequestReceived -= OnRequestReceived;
            _connection.ServiceClosed -= OnServiceClosed;
            _connection = null;

            Cancel();
        }

        public static void Cancel()
        {
            if (_deferral != null)
            {
                _deferral.Complete();
                _deferral = null;
            }
        }
    }
}
