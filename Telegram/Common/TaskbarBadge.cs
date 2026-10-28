//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Telegram.Host;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace Telegram.Common
{
    /// <summary>
    /// The unread count on the taskbar button, which BadgeUpdateManager puts there for a package
    /// and refuses to without one. An overlay icon belongs to a window rather than to the app, so
    /// it is set again whenever the main window is activated: one reopened from the notification
    /// area icon is a new window, the first one may not have its button yet when the count
    /// arrives, and the taskbar forgets every overlay when explorer restarts.
    /// </summary>
    internal static partial class TaskbarBadge
    {
        private const int SM_CXSMICON = 49;

        private static readonly Color Background = Color.FromArgb(0xFF, 0xE5, 0x39, 0x35);

        private static int _count;

        private static string _fontFamily;
        private static float _capHeight;

        private static ITaskbarList3 _taskbar;

        private static IntPtr _icon;
        private static int _iconCount;
        private static uint _iconDpi;
        private static string _description;

        /// <summary>
        /// From the debounced unread count, on a background thread.
        /// </summary>
        public static void SetCount(int count)
        {
            _count = count;
            WindowContext.Main?.Dispatcher.Dispatch(Update);
        }

        public static void OnWindowActivated(WindowContext window, bool active)
        {
            if (active && window == WindowContext.Main)
            {
                Update();
            }
        }

        private static void Update()
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
