using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace UnigramBridge
{
    class BridgeApplicationContext : ApplicationContext
    {
        private AppServiceConnection connection = null;
        private NotifyIcon notifyIcon = null;

        public BridgeApplicationContext()
        {
            MenuItem openMenuItem = new MenuItem("Open Unigram", new EventHandler(OpenApp));
            MenuItem sendMenuItem = new MenuItem("Send message to UWP", new EventHandler(SendToUWP));
            MenuItem exitMenuItem = new MenuItem("Quit Unigram", new EventHandler(Exit));
            openMenuItem.DefaultItem = true;

            notifyIcon = new NotifyIcon();
            notifyIcon.Click += new EventHandler(OpenApp);
            notifyIcon.Icon = Properties.Resources.Default;
            notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { openMenuItem, exitMenuItem });
            notifyIcon.Text = "Unigram";

            var local = ApplicationData.Current.LocalSettings;
            if (local.Values.TryGetValue("IsTrayVisible", out object value) && value is bool visible)
            {
                notifyIcon.Visible = visible;
            }
            else
            {
                notifyIcon.Visible = true;
            }

            Connect();
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            IEnumerable<AppListEntry> appListEntries = await Package.Current.GetAppListEntriesAsync();
            await appListEntries.First().LaunchAsync();

            Connect();
        }

        private async void SendToUWP(object sender, EventArgs e)
        {
            ValueSet message = new ValueSet();
            message.Add("content", "Message from Systray Extension");
            await SendToUWP(message);
        }

        private async void Exit(object sender, EventArgs e)
        {
            //ValueSet message = new ValueSet();
            //message.Add("exit", "");
            //await SendToUWP(message);

            if (connection != null)
            {
                await connection.SendMessageAsync(new ValueSet { { "Exit", string.Empty } });
            }

            notifyIcon.Dispose();
            Application.Exit();
        }

        private async Task SendToUWP(ValueSet message)
        {
            if (connection == null)
            {
                connection = new AppServiceConnection();
                connection.PackageFamilyName = Package.Current.Id.FamilyName;
                connection.AppServiceName = "org.unigram.bridge";
                connection.RequestReceived += Connection_RequestReceived;
                connection.ServiceClosed += Connection_ServiceClosed;

                var connectionStatus = await connection.OpenAsync();
                if (connectionStatus != AppServiceConnectionStatus.Success)
                {
                    //MessageBox.Show("Status: " + connectionStatus.ToString());
                    return;
                }
            }

            await connection.SendMessageAsync(message);
        }

        private async void Connect()
        {
            if (connection != null)
            {
                return;
            }

            connection = new AppServiceConnection();
            connection.PackageFamilyName = Package.Current.Id.FamilyName;
            connection.AppServiceName = "org.unigram.bridge";
            connection.RequestReceived += Connection_RequestReceived;
            connection.ServiceClosed += Connection_ServiceClosed;

            var connectionStatus = await connection.OpenAsync();
            if (connectionStatus != AppServiceConnectionStatus.Success)
            {
                //MessageBox.Show("Status: " + connectionStatus.ToString());
            }
        }

        [DllImport("user32.dll")]
        static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.TryGetValue("FlashWindow", out object flash))
            {
#if DEBUG
                var handle = FindWindow("ApplicationFrameWindow", "Telegram");
#else
                var handle = FindWindow("ApplicationFrameWindow", "Unigram");
#endif

                FlashWindow(handle, true);
            }
            else if (args.Request.Message.TryGetValue("IsTrayVisible", out object value) && value is bool visible)
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = visible;
                }
            }
            else if (args.Request.Message.TryGetValue("Exit", out object exit))
            {
                notifyIcon.Dispose();
                Application.Exit();
            }
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            connection.ServiceClosed -= Connection_ServiceClosed;
            connection = null;

            //Application.Exit();
            Connect();

            //MessageBox.Show(args.Status.ToString());
        }
    }
}
