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
using Telegram.Services;
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

        private static readonly DisposableMutex _lock = new();

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

        public static async void Connect(AppServiceConnection connection, IBackgroundTaskInstance task)
        {
            Logger.Info();

            task.Canceled += OnCanceled;

            using (_lock.Wait())
            {
                _connection = connection;
                _connection.RequestReceived += OnRequestReceived;
                _connection.ServiceClosed += OnServiceClosed;

                _deferral = task.GetDeferral();
            }

            var values = new ValueSet
            {
                { "ProcessId", Process.GetCurrentProcess().Id },
                { "OpenText", Strings.NotifyIconOpen },
                { "ExitText", Strings.NotifyIconExit }
            };

            await SendMessageAsync(values);
        }

        public static Task ExitAsync()
        {
            return SendMessageAsync("Exit");
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
            try
            {
                AppServiceResponse response = null;
                using (_lock.Wait())
                {
                    var connection = _connection;
                    if (connection == null)
                    {
                        return null;
                    }

                    if (SettingsService.Current.Diagnostics.BridgeDebug)
                    {
                        foreach (var item in message)
                        {
                            Logger.Info(item.Key);
                            break;
                        }
                    }

                    var task = connection.SendMessageAsync(message).AsTask();
                    var completed = await Task.WhenAny(task, Task.Delay(500));

                    if (task == completed)
                    {
                        response = task.Result;
                    }
                }

                if (response?.Status != AppServiceResponseStatus.Success)
                {
                    Logger.Error(response == null ? "Timeout" : response.Status);

                    if (reconnect)
                    {
                        Cancel();
                    }
                }
                else if (SettingsService.Current.Diagnostics.BridgeDebug)
                {
                    Logger.Info("Succeeded");
                }

                return response;
            }
            catch (Exception ex)
            {
                // All the remote procedure calls must be wrapped in a try-catch block

                // ToString not to send to AppCenter
                Logger.Error(ex.ToString());

                if (reconnect)
                {
                    Cancel();
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
            if (SettingsService.Current.Diagnostics.BridgeDebug)
            {
                Logger.Info();
            }

            using (_lock.Wait())
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
}
