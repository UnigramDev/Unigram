//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Telegram.Stub
{
    class BridgeApplicationContext : ApplicationContext
    {
        private AppServiceConnection _connection = null;
        private NotifyIcon _notifyIcon = null;

        private bool _closeRequested = true;
        private int _processId;

        //private InterceptKeys _intercept;

        public BridgeApplicationContext()
        {
            //_intercept = new InterceptKeys();

            SystemEvents.SessionEnded += OnSessionEnded;

            MenuItem openMenuItem = new MenuItem("Open Unigram", new EventHandler(OpenApp));
            MenuItem exitMenuItem = new MenuItem("Quit Unigram", new EventHandler(Exit));
            openMenuItem.DefaultItem = true;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Click += OpenApp;
            _notifyIcon.Icon = Properties.Resources.Default;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { openMenuItem, exitMenuItem });
#if DEBUG
            _notifyIcon.Text = "Telegram";
#else
            _notifyIcon.Text = "Unigram";
#endif

            _notifyIcon.Visible = true;

            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGetValue("IsLaunchMinimized", out object minimizedV) && minimizedV is bool minimized && !minimized)
                {
                    OpenApp(null, null);
                }
                else
                {
                    Connect();
                }
            }
            catch
            {
                _notifyIcon.Visible = true;
            }
        }

        private void OnSessionEnded(object sender, SessionEndedEventArgs e)
        {
            SystemEvents.SessionEnded -= OnSessionEnded;

            if (_connection != null)
            {
                _connection.ServiceClosed -= OnServiceClosed;
                _connection.Dispose();
                _connection = null;
            }

            if (_processId != 0)
            {
                try
                {
                    var process = Process.GetProcessById(_processId);
                    process?.Kill();
                }
                catch { }
            }

            _notifyIcon.Dispose();
            Application.Exit();
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            // There's a bug (I guess?) in NotifyIcon that causes Click handler
            // to be fired if user opens the context menu and then dismisses it.
            if (e is MouseEventArgs args && args.Button == MouseButtons.Right)
            {
                return;
            }

            try
            {
                var appListEntries = await Package.Current.GetAppListEntriesAsync();
                await appListEntries.First().LaunchAsync();
            }
            catch { }

            Connect();
        }

        private async void Exit(object sender, EventArgs e)
        {
            if (_connection != null)
            {
                _connection.ServiceClosed -= OnServiceClosed;

                try
                {
                    await _connection.SendMessageAsync(new ValueSet { { "Exit", string.Empty } });
                }
                catch
                {

                }
                finally
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }

            _notifyIcon.Dispose();
            Application.Exit();
        }

        private async void Connect()
        {
            if (_connection != null)
            {
                return;
            }

            _connection = new AppServiceConnection
            {
                PackageFamilyName = Package.Current.Id.FamilyName,
                AppServiceName = "org.telegram.bridge"
            };

            _connection.RequestReceived += OnRequestReceived;
            _connection.ServiceClosed += OnServiceClosed;

            await _connection.OpenAsync();
        }

        //[StructLayout(LayoutKind.Sequential)]
        //public struct FLASHWINFO
        //{
        //    public UInt32 cbSize;
        //    public IntPtr hwnd;
        //    public FlashWindow dwFlags;
        //    public UInt32 uCount;
        //    public UInt32 dwTimeout;
        //}

        //public enum FlashWindow : uint
        //{
        //    /// <summary>
        //    /// Stop flashing. The system restores the window to its original state.
        //    /// </summary>    
        //    FLASHW_STOP = 0,

        //    /// <summary>
        //    /// Flash the window caption
        //    /// </summary>
        //    FLASHW_CAPTION = 1,

        //    /// <summary>
        //    /// Flash the taskbar button.
        //    /// </summary>
        //    FLASHW_TRAY = 2,

        //    /// <summary>
        //    /// Flash both the window caption and taskbar button.
        //    /// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
        //    /// </summary>
        //    FLASHW_ALL = 3,

        //    /// <summary>
        //    /// Flash continuously, until the FLASHW_STOP flag is set.
        //    /// </summary>
        //    FLASHW_TIMER = 4,

        //    /// <summary>
        //    /// Flash continuously until the window comes to the foreground.
        //    /// </summary>
        //    FLASHW_TIMERNOFG = 12
        //}

        //[DllImport("user32.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        //[DllImport("user32.dll", SetLastError = true)]
        //static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();

            if (args.Request.Message.TryGetValue("ProcessId", out object process) && process is int processId)
            {
                _processId = processId;
            }
            else if (args.Request.Message.TryGetValue("FlashWindow", out object flash))
            {
                //#if DEBUG
                //                var handle = FindWindow("ApplicationFrameWindow", "Telegram");
                //#else
                //                var handle = FindWindow("ApplicationFrameWindow", "Unigram");
                //#endif

                //                FLASHWINFO info = new FLASHWINFO();
                //                info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
                //                info.hwnd = handle;
                //                info.dwFlags = FlashWindow.FLASHW_ALL;
                //                info.dwTimeout = 0;
                //                info.uCount = 1;
                //                FlashWindowEx(ref info);
            }
            else if (args.Request.Message.TryGetValue("UnreadCount", out object unread) && args.Request.Message.TryGetValue("UnreadUnmutedCount", out object unreadUnmuted))
            {
                if (unread is int unreadCount && unreadUnmuted is int unreadUnmutedCount)
                {
                    if (unreadCount > 0 || unreadUnmutedCount > 0)
                    {
                        _notifyIcon.Icon = unreadUnmutedCount > 0 ? Properties.Resources.Unmuted : Properties.Resources.Muted;
                    }
                    else
                    {
                        _notifyIcon.Icon = Properties.Resources.Default;
                    }
                }
            }
            else if (args.Request.Message.ContainsKey("LoopbackExempt"))
            {
                AddLocalhostExemption();
            }
            else if (args.Request.Message.ContainsKey("CloseRequested"))
            {
                _closeRequested = true;
            }
            else if (args.Request.Message.ContainsKey("Exit"))
            {
                _connection.ServiceClosed -= OnServiceClosed;
                _connection.Dispose();

                _notifyIcon.Dispose();
                Application.Exit();
            }

            try
            {
                await args.Request.SendResponseAsync(new ValueSet());
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

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            _connection.ServiceClosed -= OnServiceClosed;
            _connection.Dispose();
            _connection = null;

            if (_closeRequested)
            {
                _closeRequested = true;
                Connect();
            }
            else
            {
                _notifyIcon.Dispose();
                Application.Exit();
            }
        }

        private static void AddLocalhostExemption()
        {
            var familyName = Package.Current.Id.FamilyName;
            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = "CheckNetIsolation.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "LoopbackExempt -a -n=" + familyName
            };

            try
            {
                Process process = Process.Start(info);
                process.WaitForExit();
                process.Dispose();
            }
            catch { }
        }
    }
}
