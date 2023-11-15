using System;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.Core.Preview;

namespace Telegram.Common
{
    public class SystemTray
    {
        private static AppServiceConnection _connection;
        private static BackgroundTaskDeferral _deferral;

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

        public static void Connect(AppServiceConnection connection, BackgroundTaskDeferral deferral)
        {
            _connection = connection;
            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            _deferral = deferral;
        }

        public static async Task ExitAsync()
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.SendMessageAsync("Exit");
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static async Task CloseRequestedAsync()
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.SendMessageAsync("CloseRequested");
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public static async void CloseRequested(SystemNavigationCloseRequestedPreviewEventArgs args)
        {
            if (_connection != null && SettingsService.Current.Diagnostics.FullBridgeLifecycle)
            {
                var deferral = args.GetDeferral();

                try
                {
                    await _connection.SendMessageAsync("CloseRequested");
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }

        public static async void EnteringBackground(EnteredBackgroundEventArgs args)
        {
            if (_connection != null && SettingsService.Current.Diagnostics.FullBridgeLifecycle)
            {
                var deferral = args.GetDeferral();

                try
                {
                    await _connection.SendMessageAsync("CloseRequested");
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
                finally
                {
                    deferral.Complete();
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
