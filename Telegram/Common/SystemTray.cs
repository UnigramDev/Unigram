using System;
using System.Threading.Tasks;
using Telegram.Navigation;
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

        public static Task ExitAsync()
        {
            return SendAsync("Exit");
        }

        public static async void CloseRequested(SystemNavigationCloseRequestedPreviewEventArgs args)
        {
            if (_connection != null)
            {
                using (args.GetDeferral())
                {
                    await SendAsync("CloseRequested");
                }
            }
        }

        public static async void EnteringBackground(EnteredBackgroundEventArgs args)
        {
            if (_connection != null)
            {
                using (args.GetDeferral())
                {
                    await SendAsync("CloseRequested");
                }
            }
        }

        public static Task LoopbackExemptAsync(bool enabled)
        {
            return SendAsync("LoopbackExempt", enabled);
        }

        public static Task SendAsync(string message, object parameter = null)
        {
            if (_connection != null)
            {
                return _connection.SendMessageAsync(new ValueSet { { message, parameter ?? true } }).AsTask();
            }

            return Task.CompletedTask;
        }

        public static Task SendUnreadCountAsync(int unreadCount, int unreadMutedCount)
        {
            if (_connection != null)
            {
                return _connection.SendMessageAsync(new ValueSet { { "UnreadCount", unreadCount }, { "UnreadUnmutedCount", unreadMutedCount } }).AsTask();
            }

            return Task.CompletedTask;
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
