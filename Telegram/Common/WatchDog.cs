using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

        [DllImport("Telegram.Diagnostics.dll")]
        private static extern int start(uint pid, uint framework);

        static WatchDog()
        {
            _crashLog = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
            _reports = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Reports");
        }

        public static bool HasCrashedInLastSession { get; private set; }

        public static void Initialize()
        {
            NativeUtils.SetFatalErrorCallback(FatalErrorCallback);

            Client.SetLogMessageCallback(Constants.DEBUG ? 5 : 0, FatalErrorCallback);

            BootStrapper.Current.UnhandledException += (s, args) =>
            {
                if (args.Exception is LayoutCycleException)
                {
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
                }

                args.Handled = args.Exception is not LayoutCycleException;
            };

            if (Constants.DEBUG && !Debugger.IsAttached)
            {
                var process = Process.GetCurrentProcess();
                var hr = start((uint)process.Id, 1);

                var ex = Marshal.GetExceptionForHR(hr);
                if (ex != null)
                {
                    Crashes.TrackError(ex);
                }
            }

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

                    var layout = GetLayoutCyclePath();
                    if (layout.Length > 0 && File.Exists(layout))
                    {
                        data += "\n\n" + File.ReadAllText(layout);
                    }

                    return new[] { ErrorAttachmentLog.AttachmentWithText(data, "crash.txt") };
                }

                return Array.Empty<ErrorAttachmentLog>();
            };

            AppCenter.Start(Constants.AppCenterId, typeof(Analytics), typeof(Crashes));
            Analytics.TrackEvent("Windows",
                new Dictionary<string, string>
                {
                    { "DeviceFamily", AnalyticsInfo.VersionInfo.DeviceFamily },
                    { "Architecture", Package.Current.Id.Architecture.ToString() }
                });

            if (SettingsService.Current.Diagnostics.XamlDiagnostics)
            {
                var process = Process.GetCurrentProcess();
                var hr = start((uint)process.Id, 1);

                var ex = Marshal.GetExceptionForHR(hr);
                if (ex != null)
                {
                    Crashes.TrackError(ex);
                }
            }
        }

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

        public static void FatalErrorCallback(string message)
        {
            if (message.Contains("libvlc.dll") || message.Contains("libvlccore.dll"))
            {
                Crashes.TrackCrash(new LibVLCException(message));
            }
            else
            {
                Crashes.TrackCrash(new UnmanagedException(message));
            }
        }

        private static void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel != 0)
            {
#if DEBUG
                if (verbosityLevel == 1 && message.Contains("File remote location was changed from"))
                {
                    var source = Path.Combine(ApplicationData.Current.LocalFolder.Path, "tdlib_log.txt");
                    var destination = Path.ChangeExtension(source, ".backup");

                    File.Copy(source, destination, true);
                }
#endif

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
            var version = VersionLabel.GetVersion();

            var next = DateTime.Now.ToTimestamp();
            var prev = SettingsService.Current.Diagnostics.LastUpdateTime;

            var count = SettingsService.Current.Diagnostics.UpdateCount;

            var memoryUsage = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsage);
            var memoryUsageLimit = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsageLimit);

            var info =
                $"Current version: {version}\n" +
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
                var size = WindowContext.Current.Size;

                var ratio = SettingsService.Current.DialogsWidthRatio;
                var width = MasterDetailPanel.CountDialogsWidthFromRatio(size.Width, ratio);

                info += $"Screen reader: {reader}\n" +
                    $"Screen scaling: {scaling}%\n" +
                    $"Text scaling: {text}%\n" +
                    $"Window size: {size.Width}x{size.Height}\n" +
                    $"Column width: {ratio} ({width})\n";
            }

            info += $"HRESULT: 0x{exception.HResult:X4}\n" + "\n";

            var dump = Logger.Dump();
            var payload = info + dump;

            File.WriteAllText(_crashLog, reportId);
            File.WriteAllText(GetErrorReportPath(reportId), payload);
        }

        private static string GetErrorReportPath(string reportId)
        {
            Directory.CreateDirectory(_reports);
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, _reports, reportId + ".appcenter");
        }

        private static string GetLayoutCyclePath()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "LayoutCycle.txt");
        }

        public static void Suspend()
        {
            if (File.Exists(_crashLog))
            {
                File.Delete(_crashLog);
            }
        }
    }

    public class UnmanagedException : Exception
    {
        public UnmanagedException(string message)
            : base(message)
        {
        }
    }

    public class LibVLCException : Exception
    {
        public LibVLCException(string message)
            : base(message)
        {
        }
    }
}
