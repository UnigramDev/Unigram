//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Win32;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Telegram.Stub
{
    class BridgeApplicationContext
    {
        private AppServiceConnection? _connection = null;
        private readonly NotifyIcon _notifyIcon;

        private bool _closeRequested = true;
        private int _processId;

        public BridgeApplicationContext(NotifyIcon notifyIcon)
        {
            SystemEvents.SessionEnded += OnSessionEnded;

            _notifyIcon = notifyIcon;
            _notifyIcon.Click += OnClick;
            _notifyIcon.Exit += OnExit;

            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values.TryGet("IsLaunchMinimized", out bool minimized) && !minimized)
                {
                    OnClick(null, EventArgs.Empty);
                }
                else
                {
                    Connect();
                }
            }
            catch
            {
                // Can happen
            }
        }

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
        }

        private async void OnClick(object? sender, EventArgs e)
        {
            try
            {
                var appListEntries = await Package.Current.GetAppListEntriesAsync();
                await appListEntries.First().LaunchAsync();
            }
            catch { }

            Connect();
        }

        private async void OnExit(object? sender, EventArgs e)
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            Logger.Info();

            var deferral = args.GetDeferral();
            var response = new ValueSet();

            if (args.Request.Message.TryGet("ProcessId", out int processId))
            {
                Logger.Info("ProcessId");

                _processId = processId;
                response.Add("ProcessId", Environment.ProcessId);
            }

            if (args.Request.Message.TryGet("OpenText", out string? openText))
            {
                Logger.Info("OpenText");

                _notifyIcon?.UpdateOpenText(openText);
            }

            if (args.Request.Message.TryGet("ExitText", out string? exitText))
            {
                Logger.Info("ExitText");

                _notifyIcon?.UpdateExitText(exitText);
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
                    _notifyIcon?.Icon = unreadUnmutedCount > 0 ? NotifyIconIcon.Unmuted : NotifyIconIcon.Muted;
                }
                else
                {
                    _notifyIcon?.Icon = NotifyIconIcon.Default;
                }
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

                _connection?.RequestReceived -= OnRequestReceived;
                _connection?.ServiceClosed -= OnServiceClosed;
            }

            if (args.Request.Message.ContainsKey("IsPasskeySupported"))
            {
                Logger.Info("IsPasskeySupported");

                response.Add("Result", Passkeys.IsSupported());
            }

            if (args.Request.Message.TryGet("MakeCredential", out string? makeCredential))
            {
                Logger.Info("MakeCredential");

                IntPtr hWnd;
                if (args.Request.Message.TryGet("WindowId", out long windowId))
                {
                    hWnd = new IntPtr(windowId);
                }
                else
                {
                    hWnd = GetForegroundWindow();
                }

                var data = Passkeys.DeserializeRegisterData(makeCredential);
                if (data != null)
                {
                    var result = Passkeys.MakeCredential(hWnd, data);
                    if (result is Passkeys.RegisterResult register)
                    {
                        response.Add("Result", 0);
                        response.Add("ClientData", register.ClientDataJson);
                        response.Add("AttestationObject", register.AttestationObject);
                    }
                    else if (result is Exception exception)
                    {
                        response.Add("Result", exception.HResult);
                        response.Add("Message", exception.Message);
                    }
                    else
                    {
                        response.Add("Result", -1);
                        response.Add("Message", "Unknown error");
                    }
                }
                else
                {
                    response.Add("Result", -1);
                    response.Add("Message", "Failed to deserialize parameters");
                }
            }

            if (args.Request.Message.TryGet("GetAssertion", out string? getAssertion))
            {
                Logger.Info("GetAssertion");

                IntPtr hWnd;
                if (args.Request.Message.TryGet("WindowId", out long windowId))
                {
                    hWnd = new IntPtr(windowId);
                }
                else
                {
                    hWnd = GetForegroundWindow();
                }

                var data = Passkeys.DeserializeLoginData(getAssertion);
                if (data != null)
                {
                    var result = Passkeys.GetAssertion(hWnd, data);
                    if (result is Passkeys.LoginResult login)
                    {
                        response.Add("Result", 0);
                        response.Add("CredentialId", Base64Url.EncodeToString(login.CredentialId));
                        response.Add("ClientData", login.ClientDataJson);
                        response.Add("AuthenticatorData", login.AuthenticatorData);
                        response.Add("Signature", login.Signature);
                        response.Add("UserHandle", login.UserHandle);
                    }
                    else if (result is Exception exception)
                    {
                        response.Add("Result", exception.HResult);
                        response.Add("Message", exception.Message);
                    }
                    else
                    {
                        response.Add("Result", -1);
                        response.Add("Message", "Unknown error");
                    }
                }
                else
                {
                    response.Add("Result", -1);
                    response.Add("Message", "Failed to deserialize parameters");
                }
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

                _connection?.Dispose();
                _notifyIcon?.Dispose();
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            Logger.Info("_closeRequested: " + _closeRequested);

            sender.RequestReceived -= OnRequestReceived;
            sender.ServiceClosed -= OnServiceClosed;
            sender.Dispose();

            _connection = null;

            if (_closeRequested)
            {
                _closeRequested = true;
                Connect();
            }
            else
            {
                _notifyIcon.Dispose();
            }
        }
    }
}
