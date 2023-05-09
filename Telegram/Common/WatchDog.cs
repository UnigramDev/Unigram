using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.IO;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System.Profile;
using File = System.IO.File;

namespace Telegram
{
    /*
     * How does this work?
     * 
     * AppCenter SDK has been forked to provide better error tracing.
     * When a managed unhandled exception is raised, AppCenter SDK
     * will raise CreatingErrorReport, providing a report id to associate
     * the exception data with the additional logs that should be sent
     * alongside the report. When this happens, crash.log is updated 
     * using the report id.
     * 
     * On the next app startup we check if crash.log contains a report id,
     * if yes we will mark the report as a crash by returning true in 
     * ShouldProcessErrorReport.
     * 
     * We're also monitoring unmanaged exceptions by using registering
     * SetUnhandledExceptionFilter on DLL_THREAD_ATTACH from dllmain.cpp
     * 
     * Whenever an unmanaged exception is thrown, we're going to wrap it
     * into UnmanagedException, and pass it to Crashes.TrackCrash.
     * 
     */

    public class WatchDog
    {
#if DEBUG
        private static readonly bool _disabled = true;
#else
        private static readonly bool _disabled = false;
#endif

        private static readonly string _crashLog;
        private static string _lastSessionErrorReportId;
        private static bool _lastSessionTerminatedUnexpectedly;

        static WatchDog()
        {
            _crashLog = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
        }

        public static bool HasCrashedInLastSession { get; private set; }

        public static void Initialize()
        {
            if (_disabled)
            {
                return;
            }

            Read();

            NativeUtils.SetFatalErrorCallback(FatalErrorCallback);
            Client.SetLogMessageCallback(0, FatalErrorCallback);

            Crashes.CreatingErrorReport += (s, args) =>
            {
                Client.Execute(new AddLogMessage(1, "Crashes.CreatingErrorReport: " + args.ReportId));
                Track(args.ReportId, $"Unhandled exception: {args.Exception}\n\n" + NativeUtils.GetBacktrace());
            };

            Crashes.ShouldProcessErrorReport = report =>
            {
                Client.Execute(new AddLogMessage(1, "Crashes.ShouldProcessErrorReport: " + report.Id));
                return report.Id == _lastSessionErrorReportId;
            };

            Crashes.GetErrorAttachments = report =>
            {
                Client.Execute(new AddLogMessage(1, "Crashes.GetErrorAttachments: " + report.Id));

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
                    { "Architecture", Package.Current.Id.Architecture.ToString() }
                });
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
            }
        }

        private static void FatalErrorCallback(string message)
        {
            FatalErrorCallback(int.MaxValue, message);
        }

        private static void FatalErrorCallback(int verbosityLevel, string message)
        {
            if (verbosityLevel == 0)
            {
                var exception = TdException.FromMessage(message);
                if (exception.IsUnhandled)
                {
                    Crashes.TrackCrash(exception);
                }
            }
            else
            {
                Crashes.TrackCrash(new UnmanagedException(message));
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

            File.WriteAllText(_crashLog, VersionLabel.GetVersion());
        }

        private static void Track(string reportId, string data)
        {
            var version = VersionLabel.GetVersion();

            var next = DateTime.Now.ToTimestamp();
            var prev = SettingsService.Current.Diagnostics.LastUpdateTime;

            var count = SettingsService.Current.Diagnostics.UpdateCount;

            var info =
                $"Current version: {version}\n" +
                $"Time since last update: {next - prev}s\n" +
                $"Update count: {count}\n\n";

            var dump = Logger.Dump();
            var payload = data + "\n----------\n" + info + dump;

            File.WriteAllText(_crashLog, reportId);
            File.WriteAllText(GetErrorReportPath(reportId), payload);
        }

        private static string GetErrorReportPath(string reportId)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, reportId + ".appcenter");
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

    public class UnhandledException : Exception
    {
        public UnhandledException(string message)
            : base(message)
        {
        }
    }
}
