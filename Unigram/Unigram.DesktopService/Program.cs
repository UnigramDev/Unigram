using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace Unigram.DesktopService
{
    class Program
    {
        static AppServiceConnection connection = null;

        static void Main(string[] args)
        {
            Thread appServiceThread = new Thread(new ThreadStart(ThreadProc));
            appServiceThread.Start();

            // Just for debug pourposes
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("*****************************");
            Console.WriteLine("**** Classic desktop app ****");
            Console.WriteLine("*****************************");
            Console.ReadLine();

            Application.Run();
        }

        static async void ThreadProc()
        {
            connection = new AppServiceConnection();
            connection.AppServiceName = "CommunicationService";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            connection.RequestReceived += Connection_RequestReceived;

            // Still for debug pourposes
            var status = await connection.OpenAsync();
            switch (status)
            {
                case AppServiceConnectionStatus.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connection established - waiting for requests");
                    Console.WriteLine();
                    break;
                case AppServiceConnectionStatus.AppNotInstalled:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The app AppServicesProvider is not installed.");
                    return;
                case AppServiceConnectionStatus.AppUnavailable:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The app AppServicesProvider is not available.");
                    return;
                case AppServiceConnectionStatus.AppServiceUnavailable:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("The app AppServicesProvider is installed but it does not provide the app service {0}.", connection.AppServiceName));
                    return;
                case AppServiceConnectionStatus.Unknown:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("An unkown error occurred while we were trying to open an AppServiceConnection."));
                    return;
            }
        }

        private static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var request = (string)args.Request.Message["request"];
            if (request == "Flash")
            {
                args.Request.SendResponseAsync(new ValueSet()).Completed += delegate { };

#if DEBUG
                new Thread(new ThreadStart(() =>
                {
                    var form = new Form();
                    form.Text = "Removing focus from App Window";
                    form.Shown += (s, e) => SetForegroundWindow(form.Handle);
                    form.ShowDialog();
                })).Start();
#endif

                var handle = new IntPtr((long)args.Request.Message["handle"]);
                var parent = GetParent(handle);
                var result = FlashWindow.Flash(parent);
                //var handle = FindWindow("ApplicationFrameWindow", "TestFlash");
            }
        }

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

#if DEBUG
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
#endif
    }

    public static class FlashWindow
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        public static bool Flash(IntPtr hwnd)
        {
            var fi = new FLASHWINFO();
            fi.cbSize = Convert.ToUInt32(Marshal.SizeOf(fi));
            fi.hwnd = hwnd;
            fi.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fi.uCount = uint.MaxValue;
            fi.dwTimeout = 0;

            return FlashWindowEx(ref fi);
        }
    }
}
