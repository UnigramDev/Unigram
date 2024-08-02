using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using File = System.IO.File;

namespace Telegram
{
    /*
     * How does this work?
     * 
     * We use a fork of the AppCenter SDK to get more accurate error reports.
     * The end goal is to distinguish handled and unhandled exceptions,
     * as well to get some insights about unmanaged crashes that
     * would be otherwise invisible to us.
     * 
     * When the framework reports a managed unhandled exception via UnhandledException,
     * AppCenter SDK will raise CreatingErrorReport, providing a report id to associate
     * the exception data with the additional logs that should be sent alongside the report.
     * When this happens, crash.log is updated using the report id.
     * 
     * If the process terminates smoothly, we delete crash.log.
     * This happens in Application.Suspending.
     * 
     * On the subsequent app launch, we check if crash.log exist and contains a report id.
     * If this is the case, we will mark the report as a crash by returning true in 
     * ShouldProcessErrorReport.
     * 
     * We're also monitoring unmanaged exceptions by registering
     * SetUnhandledExceptionFilter on DLL_THREAD_ATTACH from Telegram.Native/dllmain.cpp.
     * Whenever an unmanaged exception is thrown, we're going to wrap it
     * into an UnmanagedException object, and pass it to Crashes.TrackCrash.
     * 
     * Symbolification of unmanaged exceptions is done manually by using CDB.exe as follows:
     * cdb -lines -z "{path to dll}" -y "{path to symbols}"
     * 
     * 0.000> u 0x{base + address}; q
     * 
     * base is 0x180000000 for x64 and 0x10000000 for x86
     * 
     */

    public class WatchDog
    {
        private static readonly bool _disabled = Constants.DEBUG;

        private static readonly string _crashLog;
        private static readonly string _reports;

        private static string _lastSessionErrorReportId;
        private static bool _lastSessionTerminatedUnexpectedly;

        static WatchDog()
        {
            _crashLog = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
            _reports = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Reports");
        }

        public static bool HasCrashedInLastSession { get; private set; }

        public static void Initialize()
        {
            NativeUtils.SetFatalErrorCallback(FatalErrorCallback);
            Client.SetLogMessageCallback(0, FatalErrorCallback);

            BootStrapper.Current.UnhandledException += OnUnhandledException;

            if (_disabled)
            {
                return;
            }

            Read();

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Crashes.TrackCrash(args.Exception);
                args.SetObserved();
            };

            //Crashes.UnhandledErrorDetected = () =>
            //{
            //    try
            //    {
            //        var error = ToException(NativeUtils.GetFatalError(false));
            //        if (error != null)
            //        {
            //            Crashes.TrackCrash(error);
            //        }

            //        return null;
            //    }
            //    catch
            //    {
            //        return null;
            //    }
            //};

            Crashes.CreatingErrorReport += (s, args) =>
            {
                Track(args.ReportId, args.Exception);
            };

            Crashes.SentErrorReport += (s, args) =>
            {
                if (File.Exists(GetErrorReportPath(args.Report.Id)))
                {
                    File.Delete(GetErrorReportPath(args.Report.Id));
                }
            };

            Crashes.ShouldProcessErrorReport = report =>
            {
                return report.Id == _lastSessionErrorReportId;
            };

            Crashes.GetErrorAttachments = report =>
            {
                var path = GetErrorReportPath(report.Id);
                if (path.Length > 0 && File.Exists(path))
                {
                    var data = File.ReadAllText(path);
                    return new[] { ErrorAttachmentLog.AttachmentWithText(data, "crash.txt") };
                }

                return Array.Empty<ErrorAttachmentLog>();
            };

            AppCenter.Start(Constants.AppCenterId, typeof(Analytics), typeof(Crashes));
            Analytics.TrackEvent("Windows",
                new Dictionary<string, string>
                {
                    { "DeviceFamily", AnalyticsInfo.VersionInfo.DeviceFamily },
                    { "Architecture", Package.Current.Id.Architecture.ToString() },
                    { "Processor", OSArchitecture().ToString() }
                });
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            args.Handled = args.Exception is not LayoutCycleException;

            if (args.Exception is LayoutCycleException)
            {
                Logger.Info("LayoutCycleException");
                Analytics.TrackEvent("LayoutCycleException");
                SettingsService.Current.Diagnostics.LastCrashWasLayoutCycle = true;
            }
            else if (args.Exception is NotSupportedException)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);

                foreach (var popup in popups)
                {
                    if (popup.Child is ToolTip tooltip)
                    {
                        tooltip.IsOpen = false;
                        tooltip.IsOpen = true;
                        tooltip.IsOpen = false;
                    }
                }

                return;
            }

        }

        public static Architecture OSArchitecture()
        {
            var handle = new IntPtr(-1);
            var wow64 = IsWow64Process2(handle, out var _, out var nativeMachine);

            if (wow64)
            {
                return nativeMachine == 0xaa64
                    ? Architecture.Arm64
                    : Architecture.X64;
            }

            return Architecture.X86;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

        public static void TrackEvent(string name)
        {
            if (_disabled)
            {
                return;
            }

            Analytics.TrackEvent(name);
        }

        private static void Read()
        {
            if (File.Exists(_crashLog))
            {
                _lastSessionTerminatedUnexpectedly = true;

                var data = File.ReadAllText(_crashLog);

                if (Guid.TryParse(data, out Guid guid))
                {
                    _lastSessionErrorReportId = guid.ToString();
                }

                File.Delete(_crashLog);
            }
        }

        class StackFrame : NativeStackFrame
        {
            private FatalErrorFrame _frame;

            public StackFrame(FatalErrorFrame frame)
            {
                _frame = frame;
            }

            public override IntPtr GetNativeIP()
            {
                return (IntPtr)_frame.NativeIP;
            }

            public override IntPtr GetNativeImageBase()
            {
                return (IntPtr)_frame.NativeImageBase;
            }
        }

        public static void FatalErrorCallback(FatalError error)
        {
            Crashes.TrackCrash(ToException(error));
        }

        private static Exception ToException(FatalError error)
        {
            if (error == null)
            {
                return null;
            }

            if (error.StackTrace.Contains("libvlc.dll") || error.StackTrace.Contains("libvlccore.dll"))
            {
                return new VLCException(error.Message + Environment.NewLine + error.StackTrace, error.StackTrace, error.Frames.Select(x => new StackFrame(x)));
            }

            return new NativeException(error.Message + Environment.NewLine + error.StackTrace, error.StackTrace, error.Frames.Select(x => new StackFrame(x)));
        }

        private static Exception ToException2(FatalError error)
        {
            if (error == null)
            {
                return null;
            }

            return new Exception(error.Message + Environment.NewLine + error.StackTrace);
        }

        private static void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel != 0)
            {
                return;
            }

            var exception = TdException.FromMessage(message);
            if (exception.IsUnhandled)
            {
                Crashes.TrackCrash(exception);
            }
        }

        public static void Launch(ApplicationExecutionState previousExecutionState)
        {
            // NotRunning: An app could be in this state because it hasn't been launched
            // since the last time the user rebooted or logged in. It can also be in this
            // state if it was running but then crashed, or because the user closed it earlier.

            HasCrashedInLastSession =
                _lastSessionErrorReportId != null
                && previousExecutionState == ApplicationExecutionState.NotRunning;
        }

        private static void Track(string reportId, Exception exception)
        {
            var report = BuildReport(exception);

            File.WriteAllText(_crashLog, reportId);
            File.WriteAllText(GetErrorReportPath(reportId), report);
        }

        public static string BuildReport(Exception exception)
        {
            var version = VersionLabel.GetVersion();
            var language = LocaleService.Current.Id;

            var next = DateTime.Now.ToTimestamp();
            var prev = SettingsService.Current.Diagnostics.LastUpdateTime;

            var count = SettingsService.Current.Diagnostics.UpdateCount;

            var memoryUsage = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsage);
            var memoryUsageLimit = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsageLimit);

            var info =
                $"Current version: {version}\n" +
                $"Current language: {language}\n" +
                $"Memory usage: {memoryUsage}\n" +
                $"Memory usage level: {MemoryManager.AppMemoryUsageLevel}\n" +
                $"Memory usage limit: {memoryUsageLimit}\n" +
                $"Time since last update: {next - prev}s\n" +
                $"Update count: {count}\n" +
                $"Tabs on the left: {SettingsService.Current.IsLeftTabsEnabled}\n";

            if (WindowContext.Current != null)
            {
                var reader = AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged);
                var scaling = (WindowContext.Current.RasterizationScale * 100).ToString("N0");
                var text = (BootStrapper.Current.TextScaleFactor * 100).ToString("N0");
                var size = Window.Current.Bounds;

                var ratio = SettingsService.Current.DialogsWidthRatio;
                var width = MasterDetailPanel.CountDialogsWidthFromRatio(size.Width, ratio);

                info += $"Screen reader: {reader}\n" +
                    $"Screen scaling: {scaling}%\n" +
                    $"Text scaling: {text}%\n" +
                    $"Window size: {size.Width}x{size.Height}\n" +
                    $"Column width: {ratio} ({width})\n";
            }

            info += $"Active call(s): {WindowContext.All.Count(x => x.IsCallInProgress)}\n";

            info += $"HRESULT: 0x{exception.HResult:X4}\n" + "\n";
            info += Environment.StackTrace + "\n\n";

            var dump = Logger.Dump();
            return info + dump;
        }

        private static string GetErrorReportPath(string reportId)
        {
            Directory.CreateDirectory(_reports);
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, _reports, reportId + ".appcenter");
        }

        public static void Suspend()
        {
            if (File.Exists(_crashLog))
            {
                File.Delete(_crashLog);
            }
        }
    }

    public class VLCException : NativeException
    {
        public VLCException(string message, string stackTrace, IEnumerable<NativeStackFrame> frames)
            : base(message, stackTrace, frames)
        {
        }
    }
}
