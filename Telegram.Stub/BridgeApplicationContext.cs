//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        private MenuItem _openMenuItem;
        private MenuItem _exitMenuItem;
        private NotifyIcon _notifyIcon = null;

        private bool _closeRequested = true;
        private int _processId;

        //private InterceptKeys _intercept;

        public BridgeApplicationContext()
        {
            //_intercept = new InterceptKeys();

            SystemEvents.SessionEnded += OnSessionEnded;

            _openMenuItem = new MenuItem("Open Unigram", new EventHandler(OpenApp));
            _exitMenuItem = new MenuItem("Quit Unigram", new EventHandler(Exit));
            _openMenuItem.DefaultItem = true;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Click += OpenApp;
            _notifyIcon.Icon = Properties.Resources.Default;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { _openMenuItem, _exitMenuItem });
#if DEBUG
            _notifyIcon.Text = "Telegram";
#else
            _notifyIcon.Text = "Unigram";
#endif

            _notifyIcon.Visible = true;

            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGet("IsLaunchMinimized", out bool minimized) && !minimized)
                {
                    OpenApp(null, null);
                }
                else
                {
                    Connect();
                }

                if (local.Values.ContainsKey("AddLocalhostExemption"))
                {
                    // Already registered
                }
                else
                {
                    AddLocalhostExemption();
                    local.Values.Add("AddLocalhostExemption", true);
                }

                //if (local.Values.ContainsKey("MigratedV2"))
                //{
                //    // Already migrated
                //}
                //else if (Migrate())
                //{
                //    local.Values.Add("MigratedV2", true);
                //}
            }
            catch
            {
                // Can happen
            }
        }

        private bool Migrate()
        {
            var destination = ApplicationData.Current.LocalFolder.Path;
            var source = destination.Replace(Package.Current.Id.FamilyName, "TelegramFZ-LLC.Unigram_1vfw5zm9jmzqy");

            var migrated = false;

            if (Directory.Exists(source))
            {
                try
                {
                    var confirm = MessageBox.Show("A previous installation of the app has been found. Do you want to migrate your accounts to this app?\n\nWARNING: secret chats will not be migrated.", "Unigram", MessageBoxButtons.YesNoCancel);
                    if (confirm != DialogResult.Yes)
                    {
                        return confirm == DialogResult.No;
                    }

                    _closeRequested = false;

                    _connection.RequestReceived -= OnRequestReceived;
                    _connection.ServiceClosed -= OnServiceClosed;

                    var current = Process.GetCurrentProcess();

                    foreach (var process in Process.GetProcesses())
                    {
                        if (process.Id == current.Id)
                        {
                            continue;
                        }

                        try
                        {
                            if (process.MainModule.FileName.Contains("1vfw5zm9jmzqy") || process.MainModule.FileName.Contains("3epzvh0nk91te"))
                            {
                                process.Kill();
                            }
                        }
                        catch
                        {
                            // It's not always possible to access MainModule
                        }
                    }

                    var accounts = Directory.GetDirectories(source);

                    foreach (var folder in accounts)
                    {
                        void Migrate(string binlog)
                        {
                            var binlogSource = Path.Combine(folder, binlog);
                            var binlogDestination = binlogSource.Replace("TelegramFZ-LLC.Unigram_1vfw5zm9jmzqy", Package.Current.Id.FamilyName);

                            if (File.Exists(binlogSource))
                            {
                                var directorty = Path.GetFileName(binlogDestination);

                                var session = Path.GetFileName(folder);
                                var container = ApplicationData.Current.LocalSettings.CreateContainer($"{session}", ApplicationDataCreateDisposition.Always);

                                container.Values["UserId"] = 1L;
                                container.Values["UseTestDC"] = binlog == "td_test.binlog";

                                Directory.CreateDirectory(directorty);
                                File.Copy(binlogSource, binlogDestination, true);
                            }
                        }

                        Migrate("td.binlog");
                        Migrate("td_test.binlog");
                    }

                    migrated = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                finally
                {
                    OpenApp(null, null);
                }
            }

            return migrated;
        }

        /*[DllImport("..\\Telegram.Diagnostics.dll")]
        public static extern int start(uint pid, uint framework);

        private void LaunchLayoutCycleMonitor(uint pid)
        {
            var process = Process.GetCurrentProcess();
            var fullPath = process.MainModule.FileName;

            var path = Path.GetDirectoryName(fullPath);
            path = Path.GetDirectoryName(path);
            path = Path.Combine(path, "Telegram.Diagnostics.dll");

            AllowAppContainerAccess(path);

            //if (!AllowAppContainerAccess(path))
            //{
            //    MessageBox.Show("AllowAppContainerAccess");
            //    return;
            //}

            var hr = start(pid, 1);

            var exception = Marshal.GetExceptionForHR(hr);
            if (exception != null)
            {
                MessageBox.Show(exception.ToString());
            }
            else
            {
                MessageBox.Show("All good");
            }
        }

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string StringSecurityDescriptor, uint StringSDRevision, out IntPtr SecurityDescriptor, out UIntPtr SecurityDescriptorSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSecurityDescriptorDacl(IntPtr pSecurityDescriptor, [MarshalAs(UnmanagedType.Bool)] out bool bDaclPresent, ref IntPtr pDacl, [MarshalAs(UnmanagedType.Bool)] out bool bDaclDefaulted);

        [DllImport("advapi32.dll")]
        public static extern int SetNamedSecurityInfo(
                    String pObjectName,
                    int ObjectType,
                    int SecurityInfo,
                    IntPtr psidOwner,
                    IntPtr psidGroup,
                    IntPtr pDacl,
                    IntPtr pSacl);

        private bool AllowAppContainerAccess(string path)
        {
            var success = ConvertStringSecurityDescriptorToSecurityDescriptor("D:(A;;GRGX;;;S-1-15-2-1)(A;;GRGX;;;S-1-15-2-2)", 1, out IntPtr sd, out UIntPtr sd_length);
            if (success)
            {
                IntPtr dacl = IntPtr.Zero;
                success = GetSecurityDescriptorDacl(sd, out bool present, ref dacl, out bool defaulted);

                if (success)
                {
                    var result = SetNamedSecurityInfo(path, 1, 4, IntPtr.Zero, IntPtr.Zero, dacl, IntPtr.Zero);
                    success = result == 0;
                }

                Marshal.FreeHGlobal(sd);
            }

            return success;
        }*/

        private void OnSessionEnded(object sender, SessionEndedEventArgs e)
        {
            SystemEvents.SessionEnded -= OnSessionEnded;

            if (_connection != null)
            {
                _connection.RequestReceived -= OnRequestReceived;
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
            _closeRequested = false;

            if (_connection != null)
            {
                _connection.RequestReceived -= OnRequestReceived;
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
            Logger.Info();

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
            Logger.Info();

            var deferral = args.GetDeferral();
            var response = new ValueSet();

            if (args.Request.Message.TryGet("ProcessId", out int processId))
            {
                Logger.Info("ProcessId");

                _processId = processId;
                response.Add("ProcessId", Process.GetCurrentProcess().Id);
            }

            if (args.Request.Message.TryGet("OpenText", out string openText))
            {
                Logger.Info("OpenText");

                _openMenuItem.Text = openText;
            }

            if (args.Request.Message.TryGet("ExitText", out string exitText))
            {
                Logger.Info("ExitText");

                _exitMenuItem.Text = exitText;
            }

            if (args.Request.Message.TryGetValue("FlashWindow", out object flash))
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

            if (args.Request.Message.TryGet("UnreadCount", out int unreadCount) && args.Request.Message.TryGet("UnreadUnmutedCount", out int unreadUnmutedCount))
            {
                Logger.Info("UnreadCount");

                if (unreadCount > 0 || unreadUnmutedCount > 0)
                {
                    _notifyIcon.Icon = unreadUnmutedCount > 0 ? Properties.Resources.Unmuted : Properties.Resources.Muted;
                }
                else
                {
                    _notifyIcon.Icon = Properties.Resources.Default;
                }
            }

            if (args.Request.Message.ContainsKey("LoopbackExempt"))
            {
                Logger.Info("LoopbackExempt");
                AddLocalhostExemption();
            }

            if (args.Request.Message.ContainsKey("CloseRequested"))
            {
                Logger.Info("CloseRequested");
                _closeRequested = true;
            }

            if (args.Request.Message.ContainsKey("Exit"))
            {
                Logger.Info("Exit");
                _closeRequested = false;

                _connection.RequestReceived -= OnRequestReceived;
                _connection.ServiceClosed -= OnServiceClosed;
            }

            if (args.Request.Message.TryGet("Debug", out string debug))
            {
                Logger.Info("Debug");
                _ = Task.Run(() => MessageBox.Show(debug));
                response.Add("Debug", debug);
            }

            try
            {
                var status = await args.Request.SendResponseAsync(response);
                if (status != AppServiceResponseStatus.Success)
                {
                    Logger.Error(status);
                }
            }
            catch
            {
                Logger.Info("Failed");

                // All the remote procedure calls must be wrapped in a try-catch block
            }
            finally
            {
                Logger.Info("Completed");
                deferral.Complete();
            }

            if (args.Request.Message.ContainsKey("Exit"))
            {
                Logger.Info("Exit");

                _connection.Dispose();
                _notifyIcon.Dispose();
                Application.Exit();
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            Logger.Info("_closeRequested: " + _closeRequested);

            _connection.RequestReceived -= OnRequestReceived;
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
