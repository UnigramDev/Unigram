//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Telegram.Controls;
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

        public static async void Connect(AppServiceConnection connection, IBackgroundTaskInstance task)
        {
            _connection = connection;
            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            _deferral = task.GetDeferral();
            task.Canceled += OnCanceled;

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

            await SendMessageAsync(values, _pendingExit == null);

            _pendingExit?.TrySetResult(true);
            _pendingExit = null;
        }

        private static Task EnqueueExitAsync()
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
                    Logger.Debug("Trying to kill");

                    var task = SendMessageAsync("Exit", reconnect: false);
                    var result = await Task.WhenAny(task, Task.Delay(200));

                    if (task != result)
                    {
                        Logger.Debug("Failed to kill");
                        await EnqueueExitAsync();
                    }
                }
                catch (Exception ex)
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                    Logger.Error(ex);
                }
            }
        }

        public static async void Debug(string value)
        {
            var response = await SendMessageAsync("Debug", value);
            if (response?.Status == AppServiceResponseStatus.Success)
            {
                if (response.Message.TryGet<string>("Debug", out var message))
                {
                    await MessagePopup.ShowAsync(message);
                }
            }
        }

        public static void LoopbackExempt(bool enabled)
        {
            _ = SendMessageAsync("LoopbackExempt", enabled);
        }

        public static void SendUnreadCount(int unreadCount, int unreadMutedCount)
        {
            _ = SendMessageAsync(new ValueSet { { "UnreadCount", unreadCount }, { "UnreadUnmutedCount", unreadMutedCount } });
        }

        private static Task<AppServiceResponse> SendMessageAsync(string message, object parameter = null, bool reconnect = true)
        {
            return SendMessageAsync(new ValueSet { { message, parameter ?? true } }, reconnect);
        }

        private static async Task<AppServiceResponse> SendMessageAsync(ValueSet message, bool reconnect = true)
        {
            if (_connection != null)
            {
                try
                {
                    var response = await _connection.SendMessageAsync(message);
                    if (response.Status != AppServiceResponseStatus.Success)
                    {
                        Logger.Error(response.Status);

                        if (reconnect)
                        {
                            Cancel();
                        }
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                    Logger.Error(ex);

                    if (reconnect)
                    {
                        Cancel();
                    }
                }
            }

            return null;
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
            Logger.Debug(args.Status);
            Cancel();
        }

        private static void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Logger.Debug(reason);
            Cancel();
        }

        private static void Cancel()
        {
            if (_connection != null)
            {
                _connection.RequestReceived -= OnRequestReceived;
                _connection.ServiceClosed -= OnServiceClosed;
                _connection = null;
            }

            if (_deferral != null)
            {
                _deferral.Complete();
                _deferral = null;
            }
        }
    }
}
