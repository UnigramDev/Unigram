using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public static class ScreenshotManager
    {
        public static async Task<BitmapImage> CaptureAsync()
        {
            var corewin = (ICoreWindow)CoreWindow.GetForCurrentThread();
            var interop = (ICoreWindowInterop)corewin;
            var hWnd = interop.WindowHandle;

            GetWindowRect(hWnd, out RECT rect);

            IntPtr hDC = GetDC(hWnd);

            IntPtr hDCMem = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hBmp = CreateCompatibleBitmap(hDC, rect.Right - rect.Left, rect.Bottom - rect.Top);

            IntPtr hOld = SelectObject(hDCMem, hBmp);
            PrintWindow(hWnd, hDCMem, 0x00000002);

            SelectObject(hDCMem, hOld);
            DeleteObject(hDCMem);

            BITMAPINFO lpbi = new BITMAPINFO();
            lpbi.biSize = (uint)Marshal.SizeOf(lpbi);

            if (0 == GetDIBits(hDC, hBmp, 0, 0, null, ref lpbi, DIB_Color_Mode.DIB_RGB_COLORS))
            {
                ReleaseDC(hWnd, hDC);
                return null;
            }

            byte[] lpPixels = new byte[lpbi.biSizeImage];

            lpbi.biBitCount = 32;
            lpbi.biCompression = 0 /*BI_RGB*/;
            lpbi.biHeight = Math.Abs(lpbi.biHeight);

            // Call GetDIBits a second time, this time to (format and) store the actual
            // bitmap data (the "pixels") in the buffer lpPixels
            if (0 == GetDIBits(hDC, hBmp, 0, (uint)lpbi.biHeight, lpPixels, ref lpbi, DIB_Color_Mode.DIB_RGB_COLORS))
            {
                ReleaseDC(hWnd, hDC);
                return null;
            }

            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.BitmapTransform.Flip = BitmapFlip.Vertical;
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)lpbi.biWidth, (uint)lpbi.biHeight, 96, 96, lpPixels);
                await encoder.FlushAsync();
                await bitmap.SetSourceAsync(stream);
            }

            ReleaseDC(hWnd, hDC);
            return bitmap;
        }


        #region Native

        [ComImport, Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }
            bool MessageHandled { set; }
        }

        enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5
        }

        enum DIB_Color_Mode : uint
        {
            DIB_RGB_COLORS = 0,
            DIB_PAL_COLORS = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] cols;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        [DllImport("gdi32.dll", EntryPoint = "GetDIBits")]
        static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, DIB_Color_Mode uUsage);

        #endregion
    }
}
