//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System;
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

    public partial class Properties : Dictionary<string, object>
    {

    }

    public partial class WatchDog
    {
        private static readonly bool _disabled = Constants.DEBUG;

        private static readonly Channel<string> _channel;
        private static readonly Task _channelTask;

        private static readonly string _reports;
        private static readonly string _crashLog;

        private static string _lastSessionErrorReportId;
        private static bool _lastSessionTerminatedUnexpectedly;

        private static readonly string _userId;
        private static readonly long _launchTime;

        private static readonly PersistentTokenBucketLimiter _limiter = new();

        static WatchDog()
        {
            _channel = Channel.CreateUnbounded<string>();
            _channelTask = Task.Run(HandleReportsAsync);

            _userId = SettingsService.Current.AnonymousUserId;
            _launchTime = MonotonicUnixTime.Now;

            _crashLog = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.id");
            _reports = Path.Combine(ApplicationData.Current.LocalFolder.Path, "ErrorReports");
        }

        public static bool HasCrashedInLastSession { get; private set; }

        public static long LaunchTime => _launchTime;

        public static string UserId => _userId;

        public static void Initialize()
        {
            NativeUtils.SetFatalErrorCallback(FatalErrorCallback);
            CoreApplication.UnhandledErrorDetected += OnUnhandledExceptionDetected;

            BootStrapper.Current.UnhandledException += OnUnhandledException;

            if (_disabled)
            {
                return;
            }

            Read();
            LoadReports();

            //TaskScheduler.UnobservedTaskException += (s, args) =>
            //{
            //    Crashes.TrackCrash(args.Exception);
            //    args.SetObserved();
            //};
        }

        private static void OnUnhandledExceptionDetected(object sender, UnhandledErrorDetectedEventArgs e)
        {
            var stowed = NativeUtils.GetStowedException();

            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                if (stowed != null)
                {
                    stowed.Type = ex.GetType().Name;
                    stowed.Message = ex.Message;
                    stowed.StackTrace = ex.StackTrace;

                    ProcessException(stowed, ex.HResult == unchecked((int)0x8001010A));
                }
                else
                {
                    ProcessException(ex, ex.HResult == unchecked((int)0x8001010A));
                }
            }
        }

        private static void ProcessException(Exception ex, bool captureAllThreads)
        {
            if (_limiter.TryConsume())
            {
                var reportId = Guid.NewGuid().ToString();
                var report = ExceptionSerializer.Serialize(ex, reportId, _userId, captureAllThreads, BuildReport(ex.HResult));

                var reportPath = GetErrorReportPath(reportId);

                File.WriteAllText(_crashLog, reportId);
                File.WriteAllText(reportPath, report);

                _channel.Writer.TryWrite(reportPath);
            }
        }

        private static void ProcessException(FatalError ex, bool captureAllThreads)
        {
            if (_limiter.TryConsume())
            {
                var reportId = Guid.NewGuid().ToString();
                var report = ExceptionSerializer.Serialize(ex, reportId, _userId, captureAllThreads, BuildReport(0));

                var reportPath = GetErrorReportPath(reportId);

                File.WriteAllText(_crashLog, reportId);
                File.WriteAllText(reportPath, report);

                _channel.Writer.TryWrite(reportPath);
            }
        }

        public static void TrackError(Exception ex)
        {
            ProcessException(ex, false);
        }

        private static void LoadReports()
        {
            try
            {
                Directory.CreateDirectory(_reports);

                var reports = Directory.GetFiles(_reports);

                foreach (var report in reports)
                {
                    _channel.Writer.TryWrite(report);
                }
            }
            catch
            {
                // If this fails for any reason we don't want the app to crash
            }
        }

        private static async Task HandleReportsAsync()
        {
            await foreach (var item in _channel.Reader.ReadAllAsync())
            {
                await HandleReportAsync(item);
            }
        }

        private static async Task HandleReportAsync(string reportPath)
        {
            try
            {
                if (Constants.DEBUG)
                {
                    return;
                }

                var report = File.ReadAllText(reportPath);
                var reportId = Path.GetFileNameWithoutExtension(reportPath);

                if (reportId == _lastSessionErrorReportId)
                {
                    var model = JsonSerializer.Deserialize(report, ErrorJsonContext.Default.ErrorReport);
                    model.Flags = 1 << 0;
                    report = JsonSerializer.Serialize(model, ErrorJsonContext.Default.ErrorReport);
                }

                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://integrations.telegram.org/ugram_crash_logs/storeCrashLog");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Constants.AppReportsId);
                request.Content = new StringContent(report);

                using var response = await client.SendAsync(request);

                var statusCode = (int)response.StatusCode;
                if (statusCode is 200 or 403 or 429)
                {
                    Cleanup(reportPath);
                }
                else
                {
                    // Otherwise we retry to send the report
                    _channel.Writer.TryWrite(reportPath);
                }
            }
            catch
            {
                Cleanup(reportPath);
            }

            static void Cleanup(string path)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // You never know...
                    }
                }
            }
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            args.Handled = args.Exception is not LayoutCycleException
                && args.Exception.HResult != unchecked((int)0x8001010A);

            if (args.Exception is LayoutCycleException)
            {
                SettingsService.Current.Diagnostics.LegacyScrollBars = true;
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

            if (SettingsService.Current.Diagnostics.ShowMemoryUsage && Window.Current?.Content?.XamlRoot != null)
            {
                _ = MessagePopup.ShowAsync(Window.Current.Content.XamlRoot, args.Exception.ToString(), "Unhandled exception", "OK");
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

        public static void TrackEvent(string name, Properties properties = null)
        {
            if (_disabled)
            {
                return;
            }

            // TODO: Not implemented
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

        public static void FatalErrorCallback(FatalError error)
        {
            ProcessException(error, false);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public static MEMORYSTATUSEX Create()
            {
                return new MEMORYSTATUSEX
                {
                    dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
                };
            }
        }

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

        [LibraryImport("kernelbase.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool GlobalMemoryStatusEx(MEMORYSTATUSEX* lpBuffer);

#else
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernelbase.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool GlobalMemoryStatusEx(MEMORYSTATUSEX* lpBuffer);
#endif

        public static unsafe void MemoryStatus()
        {
            var status = MEMORYSTATUSEX.Create();
            GlobalMemoryStatusEx(&status);

            var memoryUsage = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsage);
            var memoryUsageAvailable = FileSizeConverter.Convert((long)status.ullAvailPhys);
            var memoryUsageTotal = FileSizeConverter.Convert((long)status.ullTotalPhys);

            Logger.Debug(string.Format("Usage: {0}, available: {1}, total: {2}", memoryUsage, memoryUsageAvailable, memoryUsageTotal));
        }

        public static unsafe string BuildReport(int hresult)
        {
            var version = VersionLabel.GetVersion();
            var language = LocaleService.Current.Id;

            var next = MonotonicUnixTime.Now - _launchTime;
            var diff = TimeSpan.FromSeconds(next).ToDuration();

            var count = SettingsService.Current.Diagnostics.UpdateCount;

            var status = MEMORYSTATUSEX.Create();
            GlobalMemoryStatusEx(&status);

            var memoryUsage = FileSizeConverter.Convert((long)MemoryManager.AppMemoryUsage);
            var memoryUsageAvailable = FileSizeConverter.Convert((long)status.ullAvailPhys);
            var memoryUsageTotal = FileSizeConverter.Convert((long)status.ullTotalPhys);

            var info =
                $"Current version: {version}\n" +
                $"Current language: {language}\n" +
                $"Current duration: {diff}\n" +
                $"Memory usage: {memoryUsage}\n" +
                $"Memory available: {memoryUsageAvailable}\n" +
                $"Memory total: {memoryUsageTotal}\n" +
                $"Update count: {count}\n";

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

            info += $"HRESULT: 0x{hresult:X4}\n\n";

            var dump = Logger.Dump();
            return info + dump;
        }

        private static string GetErrorReportPath(string reportId)
        {
            Directory.CreateDirectory(_reports);
            return Path.Combine(_reports, reportId + ".json");
        }

        public static void Suspend()
        {
            if (File.Exists(_crashLog))
            {
                File.Delete(_crashLog);
            }
        }

        public class PersistentTokenBucketLimiter
        {
            private const double ONE_HOUR = 3.6e+6;

            private readonly int _maxTokens = 100;
            private readonly double _refillRate;
            private readonly object _lock = new();

            private int _currentTokens;
            private DateTime _lastRefill;

            public PersistentTokenBucketLimiter()
            {
                _refillRate = _maxTokens / ONE_HOUR;
                LoadState();
            }

            public bool TryConsume()
            {
                lock (_lock)
                {
                    Refill();

                    if (_currentTokens > 0)
                    {
                        _currentTokens--;
                        SaveState();
                        return true;
                    }

                    return false;
                }
            }

            private void Refill()
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRefill).TotalMilliseconds;
                var tokensToAdd = (int)(elapsed * _refillRate);

                if (tokensToAdd > 0)
                {
                    _currentTokens = Math.Min(_maxTokens, _currentTokens + tokensToAdd);
                    _lastRefill = now;
                }
            }

            private void LoadState()
            {
                _currentTokens = SettingsService.Current.ReportsCount;
                _lastRefill = SettingsService.Current.ReportsDate;
                Refill();
            }

            private void SaveState()
            {
                SettingsService.Current.ReportsCount = _currentTokens;
                SettingsService.Current.ReportsDate = _lastRefill;
            }
        }
    }

    public partial class VLCException : Exception
    {
        public VLCException(string message, string stackTrace)
            : base(message + "\n" + stackTrace)
        {
        }
    }

    public partial class VoipException : Exception
    {
        public VoipException(string message, string stackTrace)
            : base(message + "\n" + stackTrace)
        {
        }
    }

    public partial class NativeException : Exception
    {
        public NativeException(string message, string stackTrace)
            : base(message + "\n" + stackTrace)
        {
        }
    }
}
