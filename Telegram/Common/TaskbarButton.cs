//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Win32;
using Telegram.Controls.Media;
using Telegram.Host;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace Telegram.Common
{
    /// <summary>
    /// What the main window's taskbar button shows: the unread count, which BadgeUpdateManager
    /// puts there for a package and refuses to without one, and the download progress, which
    /// nothing puts there for us either way. Both belong to a window rather than to the app, so
    /// they are set again whenever the main window is activated: one reopened from the
    /// notification area icon is a new window, the first one may not have its button yet when
    /// the first update arrives, and the taskbar forgets both when explorer restarts.
    ///
    /// Also the player's Previous, Play/Pause and Next under the window's thumbnail. The taskbar
    /// only takes those once it has made the button, and says so with TaskbarButtonCreated - again
    /// after explorer restarts - so they are added from the main window's own messages.
    /// </summary>
    internal static partial class TaskbarButton
    {
        private const int SM_CXSMICON = 49;

        private const int TBPF_NOPROGRESS = 0;
        private const int TBPF_NORMAL = 2;

        // The bar is a few dozen pixels wide, so a finer step only costs explorer a message for
        // every chunk TDLib reports.
        private const int ProgressSteps = 100;

        private static readonly Color Background = Color.FromArgb(0xFF, 0xE5, 0x39, 0x35);

        private static int _count;

        // In steps, or -1 for no bar.
        private static int _progress = -1;
        private static int _shownProgress = -1;
        private static IntPtr _shownProgressHandle;

        private static string _fontFamily;
        private static float _capHeight;

        private static ITaskbarList3 _taskbar;

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint WM_NCDESTROY = 0x0082;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_DPICHANGED = 0x02E0;
        private const int THBN_CLICKED = 0x1800;

        private const uint THB_ICON = 0x2;
        private const uint THB_TOOLTIP = 0x4;
        private const uint THB_FLAGS = 0x8;

        private const uint THBF_ENABLED = 0x0;
        private const uint THBF_DISABLED = 0x1;
        private const uint THBF_HIDDEN = 0x8;

        private const uint PreviousId = 0;
        private const uint PlayPauseId = 1;
        private const uint NextId = 2;

        private static uint _taskbarButtonCreated;
        private static bool _watchingPlayback;

        // The window whose button has the thumbnail buttons, or zero until the taskbar has taken
        // them. Added once per button: after that they can only be changed by ThumbBarUpdateButtons.
        private static IntPtr _thumbBarHandle;

        private static string _glyphFontFamily;

        // Previous, Play, Pause, Next, for the DPI and taskbar theme they were drawn for.
        private static readonly IntPtr[] _glyphIcons = new IntPtr[4];
        private static uint _glyphDpi;
        private static bool _glyphLight;

        /// <summary>
        /// Before the window is shown, so that the subclass is there when the taskbar announces
        /// its button. Every main window, because one reopened from the notification area icon is
        /// a new HWND with a new button.
        /// </summary>
        public static void OnMainWindowCreated(IntPtr handle)
        {
            if (_taskbarButtonCreated == 0)
            {
                _taskbarButtonCreated = RegisterWindowMessageW("TaskbarButtonCreated");
            }

            if (!_watchingPlayback)
            {
                // Never removed: the playback service and this class both live as long as the
                // process.
                var playback = LifetimeService.Current.Playback;
                playback.StateChanged += OnPlaybackChanged;
                playback.SourceChanged += OnPlaybackChanged;
                playback.PlaylistChanged += OnPlaybackChanged;

                _watchingPlayback = true;
            }

            unsafe
            {
                SetWindowSubclass(handle, (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, UIntPtr, UIntPtr, IntPtr>)&OnMainWindowMessage, UIntPtr.Zero, UIntPtr.Zero);
            }

            // A window already on screen has had its button made and announced, and
            // TaskbarButtonCreated does not come again.
            if (Win32.IsWindowVisible(handle))
            {
                UpdateThumbBar(handle);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr OnMainWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
        {
            // An exception cannot cross back into user32.
            try
            {
                if (_taskbarButtonCreated != 0 && msg == _taskbarButtonCreated)
                {
                    _thumbBarHandle = IntPtr.Zero;
                    UpdateThumbBar(hwnd);
                }
                else if (msg == WM_COMMAND && (int)((wParam.ToInt64() >> 16) & 0xFFFF) == THBN_CLICKED)
                {
                    OnThumbBarClicked((uint)(wParam.ToInt64() & 0xFFFF));
                    return IntPtr.Zero;
                }
                else if (_thumbBarHandle == hwnd && (msg == WM_DPICHANGED || (msg == WM_SETTINGCHANGE && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")))
                {
                    // The icons are drawn for a DPI and for the taskbar's theme.
                    UpdateThumbBar(hwnd);
                }
                else if (msg == WM_NCDESTROY)
                {
                    unsafe
                    {
                        RemoveWindowSubclass(hwnd, (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, UIntPtr, UIntPtr, IntPtr>)&OnMainWindowMessage, id);
                    }

                    if (_thumbBarHandle == hwnd)
                    {
                        _thumbBarHandle = IntPtr.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return DefSubclassProc(hwnd, msg, wParam, lParam);
        }

        private static void OnPlaybackChanged(IPlaybackService sender, object args)
        {
            WindowContext.Main?.Dispatcher.Dispatch(UpdateThumbBar);
        }

        private static void UpdateThumbBar()
        {
            if (_thumbBarHandle != IntPtr.Zero)
            {
                UpdateThumbBar(_thumbBarHandle);
            }
        }

        /// <summary>
        /// Adds the buttons if this window's button does not have them yet, updates them otherwise.
        /// Hidden rather than removed while nothing plays, because the taskbar has no removal.
        /// </summary>
        private static unsafe void UpdateThumbBar(IntPtr hwnd)
        {
            var playback = LifetimeService.Current.Playback;
            var items = playback.Items;

            var hidden = playback.CurrentItem == null;
            var paused = playback.PlaybackState == PlaybackState.Paused;

            EnsureGlyphIcons(hwnd, out var stale);

            var buttons = stackalloc THUMBBUTTON[3];
            SetButton(ref buttons[0], PreviousId, _glyphIcons[0], Strings.AccDescrPrevious, hidden ? THBF_HIDDEN : THBF_ENABLED);
            SetButton(ref buttons[1], PlayPauseId, _glyphIcons[paused ? 1 : 2], paused ? Strings.AccActionPlay : Strings.AccActionPause, hidden ? THBF_HIDDEN : THBF_ENABLED);
            SetButton(ref buttons[2], NextId, _glyphIcons[3], Strings.Next, hidden ? THBF_HIDDEN : items?.Count > 1 ? THBF_ENABLED : THBF_DISABLED);

            try
            {
                _taskbar ??= CreateTaskbarList();

                if (_thumbBarHandle == hwnd)
                {
                    _taskbar.ThumbBarUpdateButtons(hwnd, 3, (IntPtr)buttons);
                }
                else
                {
                    try
                    {
                        _taskbar.ThumbBarAddButtons(hwnd, 3, (IntPtr)buttons);
                    }
                    catch
                    {
                        // Already added to this button, by OnMainWindowCreated before its
                        // TaskbarButtonCreated arrived. If there is no button, this throws too.
                        _taskbar.ThumbBarUpdateButtons(hwnd, 3, (IntPtr)buttons);
                    }

                    _thumbBarHandle = hwnd;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                // After the update, so the taskbar is never left holding a destroyed icon.
                foreach (var icon in stale)
                {
                    if (icon != IntPtr.Zero)
                    {
                        DestroyIcon(icon);
                    }
                }
            }
        }

        private static unsafe void SetButton(ref THUMBBUTTON button, uint id, IntPtr icon, string tip, uint flags)
        {
            button.dwMask = THB_ICON | THB_TOOLTIP | THB_FLAGS;
            button.iId = id;
            button.hIcon = icon;
            button.dwFlags = flags;

            var length = Math.Min(tip.Length, 259);

            fixed (char* destination = button.szTip)
            {
                tip.AsSpan(0, length).CopyTo(new Span<char>(destination, 260));
                destination[length] = '\0';
            }
        }

        /// <summary>
        /// Posted rather than run here: this arrives inside the main window's WndProc, and the
        /// player opens files and raises events that reach XAML.
        /// </summary>
        private static void OnThumbBarClicked(uint id)
        {
            var dispatcher = BootStrapper.Current.Dispatcher;

            switch (id)
            {
                case PreviousId:
                    dispatcher?.Dispatch(OnPreviousClicked);
                    break;
                case PlayPauseId:
                    dispatcher?.Dispatch(OnPlayPauseClicked);
                    break;
                case NextId:
                    dispatcher?.Dispatch(OnNextClicked);
                    break;
            }
        }

        // The player header's own three, so the buttons under the thumbnail do what its do.
        private static void OnPreviousClicked()
        {
            var playback = LifetimeService.Current.Playback;
            if (playback.Position.TotalSeconds > 5)
            {
                playback.Seek(TimeSpan.Zero);
            }
            else
            {
                playback.MovePrevious();
            }
        }

        private static void OnPlayPauseClicked()
        {
            var playback = LifetimeService.Current.Playback;
            if (playback.PlaybackState == PlaybackState.Paused)
            {
                playback.Play();
            }
            else
            {
                playback.Pause();
            }
        }

        private static void OnNextClicked()
        {
            LifetimeService.Current.Playback.MoveNext();
        }

        /// <summary>
        /// The thumbnail toolbar sits on the taskbar's own background, which follows the system
        /// theme rather than the app's, so the glyphs are drawn in that theme's text colour.
        /// What the previous set was is handed back to be destroyed once the taskbar has the new.
        /// </summary>
        private static void EnsureGlyphIcons(IntPtr hwnd, out IntPtr[] stale)
        {
            var dpi = Win32.GetDpiForWindow(hwnd);
            var light = IsTaskbarLight();

            if (dpi == _glyphDpi && light == _glyphLight && _glyphIcons[0] != IntPtr.Zero)
            {
                stale = Array.Empty<IntPtr>();
                return;
            }

            stale = (IntPtr[])_glyphIcons.Clone();

            ResolveGlyphFont();

            var size = Win32.GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            var foreground = light ? Colors.Black : Colors.White;

            _glyphIcons[0] = CreateGlyphIcon("\uE892", size, foreground);
            _glyphIcons[1] = CreateGlyphIcon(Icons.Play, size, foreground);
            _glyphIcons[2] = CreateGlyphIcon(Icons.Pause, size, foreground);
            _glyphIcons[3] = CreateGlyphIcon("\uE893", size, foreground);

            _glyphDpi = dpi;
            _glyphLight = light;
        }

        private static bool IsTaskbarLight()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr CreateGlyphIcon(string glyph, int size, Color foreground)
        {
            byte[] pixels;

            using (var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), size, size, 96))
            {
                using (var session = target.CreateDrawingSession())
                using (var format = new CanvasTextFormat())
                {
                    format.FontFamily = _glyphFontFamily;
                    format.FontSize = size;
                    format.WordWrapping = CanvasWordWrapping.NoWrap;
                    format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                    format.VerticalAlignment = CanvasVerticalAlignment.Center;

                    session.Clear(Colors.Transparent);
                    session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
                    session.DrawText(glyph, new Rect(0, 0, size, size), foreground, format);
                }

                pixels = target.GetPixelBytes();
            }

            return CreateIcon(pixels, size);
        }

        /// <summary>
        /// The system's icon font rather than the app's: these sit in the taskbar, beside the
        /// system's own, and the four glyphs are at the same code points in both.
        /// </summary>
        private static void ResolveGlyphFont()
        {
            if (_glyphFontFamily != null)
            {
                return;
            }

            var system = CanvasFontSet.GetSystemFontSet();

            foreach (var family in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                using var matching = system.GetMatchingFonts(family, FontWeights.Normal, FontStretch.Normal, FontStyle.Normal);
                if (matching.Fonts.Count > 0)
                {
                    _glyphFontFamily = family;
                    return;
                }
            }

            _glyphFontFamily = "Segoe MDL2 Assets";
        }

        private static IntPtr _icon;
        private static int _iconCount;
        private static uint _iconDpi;
        private static string _description;

        /// <summary>
        /// From the debounced unread count, on a background thread. Unpackaged only: a package's
        /// badge is BadgeUpdateManager's.
        /// </summary>
        public static void SetCount(int count)
        {
            _count = count;
            WindowContext.Main?.Dispatcher.Dispatch(UpdateBadge);
        }

        /// <summary>
        /// From the same updateFileDownloads that drives DownloadsFooter, on the UI thread. A
        /// finished list keeps its total until it is cleared, so only an unfinished one shows a bar.
        /// </summary>
        public static void SetProgress(long downloadedSize, long totalSize)
        {
            var progress = totalSize > 0 && downloadedSize < totalSize
                ? (int)((double)Math.Max(downloadedSize, 0) / totalSize * ProgressSteps)
                : -1;

            if (progress != _progress)
            {
                _progress = progress;
                WindowContext.Main?.Dispatcher.Dispatch(UpdateProgress);
            }
        }

        public static void OnWindowActivated(WindowContext window, bool active)
        {
            if (active && window == WindowContext.Main)
            {
                if (!ApiInfo.HasPackageIdentity)
                {
                    UpdateBadge();
                }

                _shownProgressHandle = IntPtr.Zero;
                UpdateProgress();
            }
        }

        private static void UpdateProgress()
        {
            if (WindowContext.Main is not WindowContext window)
            {
                return;
            }

            var handle = new IntPtr(window.Handle);
            var progress = _progress;

            if (handle == _shownProgressHandle && progress == _shownProgress)
            {
                return;
            }

            try
            {
                _taskbar ??= CreateTaskbarList();

                if (progress < 0)
                {
                    _taskbar.SetProgressState(handle, TBPF_NOPROGRESS);
                }
                else
                {
                    if (_shownProgress < 0 || handle != _shownProgressHandle)
                    {
                        _taskbar.SetProgressState(handle, TBPF_NORMAL);
                    }

                    _taskbar.SetProgressValue(handle, (ulong)progress, ProgressSteps);
                }

                _shownProgress = progress;
                _shownProgressHandle = handle;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private static void UpdateBadge()
        {
            if (WindowContext.Main is not WindowContext window)
            {
                return;
            }

            var handle = new IntPtr(window.Handle);
            var count = _count;

            try
            {
                var dpi = Win32.GetDpiForWindow(handle);
                if (count != _iconCount || dpi != _iconDpi)
                {
                    if (_icon != IntPtr.Zero)
                    {
                        DestroyIcon(_icon);
                        _icon = IntPtr.Zero;
                    }

                    if (count > 0)
                    {
                        _icon = CreateIcon(count, dpi);
                        _description = Locale.Declension(Strings.R.NewMessages, count);
                    }

                    _iconCount = count;
                    _iconDpi = dpi;
                }

                _taskbar ??= CreateTaskbarList();
                _taskbar.SetOverlayIcon(handle, _icon, _icon != IntPtr.Zero ? _description : null);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private static IntPtr CreateIcon(int count, uint dpi)
        {
            // The small icon size is what the taskbar draws an overlay at, whatever it is given.
            var size = Win32.GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            var text = count > 99 ? "99+" : count.ToString();

            byte[] pixels;

            using (var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), size, size, 96))
            {
                using (var session = target.CreateDrawingSession())
                using (var format = new CanvasTextFormat())
                using (var typography = new CanvasTypography())
                {
                    ResolveFont();

                    // The overlay is a square, so the pill the taskbar draws for a package's badge
                    // cannot grow sideways here: one or two digits fill the square at BadgeControl's
                    // 11 on 16, two in a squarer shape so they fit, and "99+" drops to a smaller
                    // pill in the corner.
                    format.FontFamily = _fontFamily;
                    format.WordWrapping = CanvasWordWrapping.NoWrap;
                    format.FontSize = size * (text.Length < 3 ? 11f / 16f : 8f / 16f);

                    typography.AddFeature(CanvasTypographyFeatureName.TabularFigures, 1);

                    using var layout = new CanvasTextLayout(session, text, format, 0, 0);
                    layout.SetTypography(0, text.Length, typography);

                    var ink = layout.DrawBounds;

                    float left = 0, top = 0, width = size, height = size, radius;

                    if (text.Length == 1)
                    {
                        radius = size / 2f;
                    }
                    else if (text.Length == 2)
                    {
                        radius = size * 7f / 16f;
                    }
                    else
                    {
                        width = MathF.Min(size, MathF.Round((float)ink.Width + size * 3f / 16f));
                        height = MathF.Round(size * 11f / 16f);
                        left = size - width;
                        top = size - height;
                        radius = size * 3f / 16f;
                    }

                    // BadgeControl's recipe: TextLineBounds.Tight makes the line box run from the
                    // cap height to the baseline, and TrimSideBearings makes it as wide as the ink,
                    // so that box is what gets centred, its corner on a whole pixel as layout
                    // rounding puts it.
                    var capHeight = _capHeight * format.FontSize;
                    var x = MathF.Round(left + (float)(width - ink.Width) / 2) - (float)ink.X;
                    var y = MathF.Round(top + (height - capHeight) / 2) + capHeight - layout.LineMetrics[0].Baseline;

                    session.Clear(Colors.Transparent);
                    session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
                    session.FillRoundedRectangle(left, top, width, height, radius, radius, Background);
                    session.DrawTextLayout(layout, x, y, Colors.White);
                }

                pixels = target.GetPixelBytes();
            }

            return CreateIcon(pixels, size);
        }

        private static IntPtr CreateIcon(byte[] pixels, int size)
        {
            // Win2D's are premultiplied, an icon's are not.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                if (alpha is > 0 and < 255)
                {
                    pixels[i + 0] = (byte)(pixels[i + 0] * 255 / alpha);
                    pixels[i + 1] = (byte)(pixels[i + 1] * 255 / alpha);
                    pixels[i + 2] = (byte)(pixels[i + 2] * 255 / alpha);
                }
            }

            var color = CreateBitmap(size, size, 1, 32, pixels);
            var mask = CreateBitmap(size, size, 1, 1, null);

            try
            {
                // The mask is ignored: a 32-bit colour bitmap carries its own alpha.
                var info = new ICONINFO
                {
                    fIcon = 1,
                    hbmMask = mask,
                    hbmColor = color
                };

                return CreateIconIndirect(info);
            }
            finally
            {
                DeleteObject(color);
                DeleteObject(mask);
            }
        }

        /// <summary>
        /// XAML's default text font, falling back to its predecessor where it is not installed.
        /// </summary>
        private static void ResolveFont()
        {
            if (_fontFamily != null)
            {
                return;
            }

            var system = CanvasFontSet.GetSystemFontSet();

            foreach (var family in new[] { "Segoe UI Variable Text", "Segoe UI" })
            {
                using var matching = system.GetMatchingFonts(family, FontWeights.Normal, FontStretch.Normal, FontStyle.Normal);
                if (matching.Fonts.Count > 0)
                {
                    _fontFamily = family;
                    _capHeight = matching.Fonts[0].CapHeight;
                    return;
                }
            }

            _fontFamily = "Segoe UI";
            _capHeight = 0.7f;
        }

        private static ITaskbarList3 CreateTaskbarList()
        {
            var clsid = new Guid("56FDF344-FD6D-11D0-958A-006097C9A090");
            var iid = new Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF");

            Marshal.ThrowExceptionForHR(CoCreateInstance(clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, iid, out var unknown));

            try
            {
                var taskbar = (ITaskbarList3)new StrategyBasedComWrappers().GetOrCreateObjectForComInstance(unknown, CreateObjectFlags.None);
                taskbar.HrInit();
                return taskbar;
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public int fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [LibraryImport("ole32.dll")]
        private static partial int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

        [LibraryImport("gdi32.dll")]
        private static partial IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, byte[] lpBits);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DeleteObject(IntPtr ho);

        [LibraryImport("user32.dll")]
        private static partial IntPtr CreateIconIndirect(in ICONINFO piconinfo);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyIcon(IntPtr hIcon);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint RegisterWindowMessageW(string lpString);

        // The System32 comctl32, version 5.82, exports these three by name, so they need no
        // common controls 6 manifest.
        [LibraryImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [LibraryImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RemoveWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, UIntPtr uIdSubclass);

        [LibraryImport("comctl32.dll")]
        private static partial IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct THUMBBUTTON
        {
            public uint dwMask;
            public uint iId;
            public uint iBitmap;
            public IntPtr hIcon;
            public fixed char szTip[260];
            public uint dwFlags;
        }

        // ITaskbarList, ITaskbarList2 and the start of ITaskbarList3, flattened: only the vtable
        // prefix up to SetOverlayIcon is declared, so nothing after it may be added out of order.
        [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
        [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
        internal partial interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, int fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, int flags);
            void RegisterTab(IntPtr tab, IntPtr mdi);
            void UnregisterTab(IntPtr tab);
            void SetTabOrder(IntPtr tab, IntPtr insertBefore);
            void SetTabActive(IntPtr tab, IntPtr mdi, uint reserved);
            void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
            void SetOverlayIcon(IntPtr hwnd, IntPtr icon, string description);
        }
    }
}
#endif
