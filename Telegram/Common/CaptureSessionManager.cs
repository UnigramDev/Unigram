using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Views.Popups;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.Display;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.UI;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public record CaptureSessionOptions(GraphicsCaptureItem CaptureItem, long ProcessId);

    public abstract record CaptureSessionItem(string DisplayName)
    {
        public abstract CaptureSessionOptions ToOptions(bool includeAudio);
    }

    public record WindowCaptureSessionItem(string DisplayName, WindowId WindowId) : CaptureSessionItem(DisplayName)
    {
        public override CaptureSessionOptions ToOptions(bool includeAudio)
        {
            var item = GraphicsCaptureItem.TryCreateFromWindowId(WindowId);
            if (item != null)
            {
                if (includeAudio)
                {
                    return new CaptureSessionOptions(item, CaptureSessionManager.GetWindowProcessId(WindowId));
                }
                else
                {
                    return new CaptureSessionOptions(item, 0);
                }
            }

            return null;
        }
    }

    public record DisplayCaptureSessionItem(GraphicsCaptureItem Item, DisplayId DisplayId) : CaptureSessionItem(Item.DisplayName)
    {
        public static implicit operator GraphicsCaptureItem(DisplayCaptureSessionItem d) => d.Item;

        public override CaptureSessionOptions ToOptions(bool includeAudio)
        {
            return new CaptureSessionOptions(Item, includeAudio ? -1 : 0);
        }
    }

    public partial class CaptureSessionManager
    {
        #region Choose

        public static async Task<CaptureSessionOptions> ChooseAsync(XamlRoot xamlRoot, bool canShareAudio)
        {
            var access = await CaptureSessionManager.RequestAccessAsync();
            if (access == AppCapabilityAccessStatus.UserPromptRequired)
            {
                var picker = new GraphicsCapturePicker();

                var backup = await picker.PickSingleItemAsync();
                if (backup != null)
                {
                    return new CaptureSessionOptions(backup, 0);
                }

                return null;
            }
            else if (access != AppCapabilityAccessStatus.Allowed)
            {
                return null;
            }

            var popup = new ChooseCapturePopup(canShareAudio);

            var confirm = await popup.ShowQueuedAsync(xamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            if (popup.SelectedItem is CaptureSessionItem item)
            {
                return item.ToOptions(popup.IsAudioCaptureEnabled);
            }

            return null;
        }

        #endregion

        public static async Task<AppCapabilityAccessStatus> RequestAccessAsync()
        {
            if (ApiInfo.IsBuildOrGreater(20348))
            {
                var capability = AppCapability.Create("graphicsCaptureProgrammatic");
                var status = capability.CheckAccess();

                if (status == AppCapabilityAccessStatus.UserPromptRequired)
                {
                    var access = await AppCapability.RequestAccessForCapabilitiesAsync(new[] { "graphicsCaptureProgrammatic" });
                    if (access.TryGetValue("graphicsCaptureProgrammatic", out status))
                    {
                        return status;
                    }
                }
                else if (status != AppCapabilityAccessStatus.Allowed)
                {
                    status = AppCapabilityAccessStatus.UserPromptRequired;
                }

                return status;
            }

            return AppCapabilityAccessStatus.UserPromptRequired;
        }

        public static uint GetWindowProcessId(WindowId windowId)
        {
            GetWindowThreadProcessId((IntPtr)windowId.Value, out uint processId);
            return processId;
        }

        public static IList<CaptureSessionItem> FindAll()
        {
            var items = new List<CaptureSessionItem>();
            items.AddRange(FindAllDisplayIds());
            items.AddRange(FindAllTopLevelWindowIds());

            return items;
        }

        public static IList<CaptureSessionItem> FindAllTopLevelWindowIds()
        {
            var windows = WindowServices.FindAllTopLevelWindowIds();
            var items = new List<CaptureSessionItem>();

            var current = (ICoreWindowInterop)(object)Window.Current.CoreWindow;
            var hWnd = (ulong)GetParent(current.WindowHandle);

            foreach (var window in windows)
            {
                if (window.Value == hWnd)
                {
                    continue;
                }

                var item = GetCaptureSessionItem(window);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public static IList<CaptureSessionItem> FindAllDisplayIds()
        {
            var displays = DisplayServices.FindAll();
            var items = new List<CaptureSessionItem>();

            foreach (var display in displays)
            {
                var item = GraphicsCaptureItem.TryCreateFromDisplayId(display);
                if (item != null)
                {
                    items.Add(new DisplayCaptureSessionItem(item, display));
                }
            }

            return items;
        }

        private static bool IsWindowVisibleOnCurrentDesktop(IntPtr window)
        {
            return IsWindowValidAndVisible(window) && /*IsWindowOnCurrentDesktop(manager, hwnd) &&*/ !IsWindowCloaked(window);
        }

        private static bool IsWindowValidAndVisible(IntPtr window)
        {
            return IsWindow(window) && IsWindowVisible(window) && !IsIconic(window);
        }

        // A cloaked window is composited but not visible to the user.
        // Example: Cortana or the Action Center when collapsed.
        private static bool IsWindowCloaked(IntPtr hwnd)
        {
            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out bool res, 0) != 0)
            {
                // Cannot tell so assume not cloaked for backward compatibility.
                return false;
            }

            return res;
        }

        private static CaptureSessionItem GetCaptureSessionItem(WindowId windowId)
        {
            var hwnd = (IntPtr)windowId.Value;

            // Skip invisible and minimized windows
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                return null;
            }

            // Skip windows which are not presented in the taskbar,
            // namely owned window if they don't have the app window style set
            IntPtr owner = GetWindow(hwnd, GW_OWNER);
            long exstyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (owner != IntPtr.Zero && (exstyle & WS_EX_APPWINDOW) == 0)
            {
                return null;
            }

            // Filter out windows that match the extended styles the caller has specified,
            // e.g. WS_EX_TOOLWINDOW for capturers that don't support overlay windows.
            if ((exstyle & WS_EX_TOOLWINDOW) != 0)
            {
                return null;
            }

            if (/*param.IgnoreUnresponsive &&*/ !IsWindowResponding(hwnd))
            {
                return null;
            }

            // GetWindowText* are potentially blocking operations if `hwnd` is
            // owned by the current process. The APIs will send messages to the window's
            // message loop, and if the message loop is waiting on this operation we will
            // enter a deadlock.
            // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowtexta#remarks
            //
            // To help consumers avoid this, there is a DesktopCaptureOption to ignore
            // windows owned by the current process. Consumers should either ensure that
            // the thread running their message loop never waits on this operation, or use
            // the option to exclude these windows from the source list.
            bool owned_by_current_process = IsWindowOwnedByCurrentProcess(hwnd);
            //if (owned_by_current_process && param.IgnoreCurrentProcessWindows)
            //{
            //    return true;
            //}

            string title = null;

            // Even if consumers request to enumerate windows owned by the current
            // process, we should not call GetWindowText* on unresponsive windows owned by
            // the current process because we will hang. Unfortunately, we could still
            // hang if the window becomes unresponsive after this check, hence the option
            // to avoid these completely.
            if (!owned_by_current_process || IsWindowResponding(hwnd))
            {
                var window_title = new StringBuilder(500);
                if (GetWindowTextLength(hwnd) != 0 &&
                    GetWindowText(hwnd, window_title, 500) > 0)
                {
                    title = window_title.ToString();
                }
            }

            // Skip windows when we failed to convert the title or it is empty.
            if (/*param.IgnoreUntitled &&*/ string.IsNullOrEmpty(title))
            {
                return null;
            }

            // Capture the window class name, to allow specific window classes to be
            // skipped.
            //
            // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassa
            // says lpszClassName field in WNDCLASS is limited by 256 symbols, so we don't
            // need to have a buffer bigger than that.
            int kMaxClassNameLength = 256;
            var class_name = new StringBuilder(kMaxClassNameLength);
            int class_name_length = GetClassName(hwnd, class_name, kMaxClassNameLength);
            if (class_name_length < 1)
            {
                return null;
            }

            // Skip Program Manager window.
            if ("Progman" == class_name.ToString() || "Windows.UI.Core.CoreWindow" == class_name.ToString())
            {
                return null;
            }

            if (IsWindowVisibleOnCurrentDesktop(hwnd))
            {
                return new WindowCaptureSessionItem(title, windowId);
            }

            return null;
        }

        #region Native

        static bool IsWindowOwnedByCurrentProcess(IntPtr hwnd)
        {
            uint process_id;
            GetWindowThreadProcessId(hwnd, out process_id);
            return process_id == GetCurrentProcessId();
        }

        static bool IsWindowResponding(IntPtr window)
        {
            // 50ms is chosen in case the system is under heavy load, but it's also not
            // too long to delay window enumeration considerably.
            uint uTimeoutMs = 50;
            return SendMessageTimeout(window, WM_NULL, UIntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, uTimeoutMs, out _) != IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("KERNEL32.dll", ExactSpelling = true)]
        static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        const int DWMWA_CLOAKED = 14;

        const uint GW_OWNER = 4;

        const int GWL_EXSTYLE = -20;

        const long WS_EX_APPWINDOW = 0x00040000L;

        const long WS_EX_TOOLWINDOW = 0x00000080L;

        const uint WM_NULL = 0x0000;

        const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll")]
        static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        #endregion
    }
}
