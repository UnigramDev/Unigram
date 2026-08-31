//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            _userId = AppSettings.AnonymousUserId;
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

#if NET9_0_OR_GREATER
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
#endif

            Read();
            LoadReports();

            //TaskScheduler.UnobservedTaskException += (s, args) =>
            //{
            //    Crashes.TrackCrash(args.Exception);
            //    args.SetObserved();
            //};
        }

        // What OnUnhandledExceptionDetected just wrote. The fail-fast hook reports the same crash
        // microseconds later on this same thread, with the stowed records this handler cannot
        // reach - every report reaches the backend, so the richer one has to replace this report
        // rather than describe the crash a second time. Thread-static because the whole sequence
        // runs on the thread that raised the error; the timestamp is because nothing clears these
        // when no fail-fast follows and the app simply carries on.
        [ThreadStatic]
        private static string _supersedeId;
        [ThreadStatic]
        private static string _supersedeType;
        [ThreadStatic]
        private static string _supersedeMessage;
        [ThreadStatic]
        private static long _supersedeTime;

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

                    // GetStowedException puts the originating description here when it can recover
                    // one, and the cases where it can are exactly the ones where ex is a bare
                    // E_FAIL - so keep both rather than letting the empty one win.
                    stowed.StackTrace = string.IsNullOrEmpty(stowed.StackTrace)
                        ? ex.StackTrace
                        : stowed.StackTrace + "\n" + ex.StackTrace;

                    // The two carry different stacks and either can be the useful one. The record
                    // is combase's capture at the ABI boundary, so for anything that reached here
                    // through an async rethrow it shows the rethrow and not the origin; ex still
                    // holds the origin, because the runtime accumulates frames into the exception
                    // across every rethrow. The other way round, ex is often nothing but Propagate
                    // rethrowing a bare E_FAIL, and then the record is all there is.
                    //
                    // So attach both and let the dashboard see the pair, rather than picking.
                    AttachManagedFrames(stowed, ex);

                    Supersede(ProcessException(stowed, defer: true), stowed.Type, stowed.Message);
                }
                else
                {
                    Supersede(ProcessException(ex, defer: true), ex.GetType().Name, ex.Message);
                }
            }
        }

        /// <summary>
        /// Hangs the managed exception's own frames off <paramref name="stowed"/> as an inner
        /// record, so a report carries both stacks instead of whichever one happened to be reached
        /// first.
        /// </summary>
        /// <remarks>
        /// Under NativeAOT the frames on an exception have no method behind them - there is no
        /// reflection metadata - but they do have an image base and an address, which is exactly
        /// what the symbolicator resolves. That is the same shape the stowed records carry, so the
        /// two travel through the report as one chain.
        ///
        /// The innermost record is where the reader looks first, so put the managed frames there
        /// only when the stowed ones cannot be the origin: a text-form record has no frames at all,
        /// and a record raised on another thread - zero, for one marshalled back over RPC - belongs
        /// to a different failure. Anything else keeps the record's own order.
        /// </remarks>
        private static void AttachManagedFrames(FatalError stowed, Exception ex)
        {
            var frames = new StackTrace(ex, true).GetFrames();
            if (frames == null || frames.Length == 0 || !frames[0].HasNativeImage())
            {
                return;
            }

            var managed = NativeUtils.CreateError(ex.GetType().Name, ex.Message, ex.StackTrace);

            foreach (var frame in frames)
            {
                managed.Frames.Add(new FatalErrorFrame
                {
                    NativeIP = frame.GetNativeIP().ToInt64(),
                    NativeImageBase = frame.GetNativeImageBase().ToInt64()
                });
            }

            var suspect = stowed.Frames.Count == 0 || stowed.ThreadId != NativeUtils.GetCurrentThreadId();
            if (suspect)
            {
                // First of the record's inner exceptions, ahead of whatever it already nested.
                // The outer stays the record either way - its type and message were replaced with
                // the exception's a moment ago, so only the frames are in question here.
                managed.InnerException = stowed.InnerException;
                stowed.InnerException = managed;
            }
            else
            {
                var last = stowed;
                while (last.InnerException != null)
                {
                    last = last.InnerException;
                }

                last.InnerException = managed;
            }
        }

#if NET9_0_OR_GREATER
        // Re-entrancy: serialising a report throws often enough on its own, and a handler that
        // reports its own failure never returns.
        [ThreadStatic]
        private static bool _reporting;

        // One report per distinct failure. Some of these fire once per frame - the video position
        // indicator managed 176 in a single session - and the point is to learn the exception
        // exists, not to count it.
        private static readonly HashSet<string> _firstChance = new();

        // These draw on the token bucket real crashes use, 100 an hour, and a message can carry a
        // path or an id that makes every occurrence a new signature. Cap the session so a
        // first-chance flood cannot starve the report for an actual crash.
        private const int MaxFirstChanceReports = 8;

        /// <summary>
        /// Reports the marshalling failures that no unhandled handler can see.
        /// </summary>
        /// <remarks>
        /// Thrown inside a CCW callback, a managed exception never reaches UnhandledErrorDetected or
        /// Application.UnhandledException: CsWinRT has to convert it to an HRESULT at the ABI
        /// boundary, so the runtime counts it as handled. XAML then fails fast on the HRESULT, and
        /// a fail-fast bypasses the native filter in dllmain.cpp as well - which is the whole point
        /// of one. First chance is the only moment the exception is still an exception.
        ///
        /// Reports are written synchronously, so the record survives the fail-fast that follows.
        /// </remarks>
        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            // Narrow on purpose: these are the types CsWinRT throws when it cannot marshal
            // something, and reporting everything would bury the real crashes in the dashboard
            // under exceptions the app goes on to handle.
            // RPC_E_WRONG_THREAD joins them for a different reason: it is survivable, so it arrives
            // through UnhandledErrorDetected with nothing but Propagate's own frames, and no
            // fail-fast follows to leave stowed records behind. Its message is constant, so the
            // dedup below spends exactly one report per session on it.
            if (_reporting || e.Exception is not (NotSupportedException or InvalidCastException
                or COMException { HResult: unchecked((int)0x8001010E) }))
            {
                return;
            }

            lock (_firstChance)
            {
                if (_firstChance.Count >= MaxFirstChanceReports
                    || !_firstChance.Add(e.Exception.GetType().Name + ": " + e.Exception.Message))
                {
                    return;
                }
            }

            _reporting = true;

            try
            {
                // The exception carries no stack yet - first chance is raised before one is
                // captured - so take the native backtrace, which at this point is the throw site.
                // The stowed exception is not an option: the CCW creates that on the way out, after
                // this has run.
                ProcessException(NativeUtils.GetBackTrace(e.Exception.GetType().Name, e.Exception.Message));
            }
            catch
            {
                // Nothing useful to do here, and throwing would take the process with it.
            }
            finally
            {
                _reporting = false;
            }
        }
#endif

        private static string ProcessException(Exception ex, bool defer = false)
        {
            if (_limiter.TryConsume())
            {
                var reportId = Guid.NewGuid().ToString();
                var report = ExceptionSerializer.Serialize(ex, reportId, _userId, BuildReport(ex.HResult));

                var reportPath = GetErrorReportPath(reportId);

                // crash.id names the report to blame if the process dies next, so every report
                // writes it and the newest wins. Suspend deletes it, and that is the only
                // proof an error was survivable - no handler can know it at the time.
                File.WriteAllText(_crashLog, reportId);

                File.WriteAllText(reportPath, report);

                Queue(reportPath, defer);

                return reportId;
            }

            return null;
        }

        private static string ProcessException(FatalError ex, string supersede = null, bool defer = false)
        {
            // A superseding report overwrites one already on disk, so it needs neither a token
            // nor a new id: reusing both is what stops it becoming a second report.
            if (supersede != null || _limiter.TryConsume())
            {
                var reportId = supersede ?? Guid.NewGuid().ToString();
                var report = ExceptionSerializer.Serialize(ex, reportId, _userId, BuildReport(0));

                var reportPath = GetErrorReportPath(reportId);

                File.WriteAllText(_crashLog, reportId);

                File.WriteAllText(reportPath, report);

                Queue(reportPath, defer);

                return reportId;
            }

            return null;
        }

        // Reports go out as soon as they are queued, and the fail-fast hook only supersedes this
        // one a moment later - long enough for the thin report to reach the backend first and for
        // the crash to be described twice. Holding the queue write back leaves whichever report
        // wins on disk as the one that gets sent; if the process dies first, LoadReports picks the
        // file up on the next launch, so nothing is lost by waiting.
        private static void Queue(string reportPath, bool defer)
        {
            if (defer)
            {
                _ = QueueDeferredAsync(reportPath);
            }
            else
            {
                _channel.Writer.TryWrite(reportPath);
            }
        }

        private static async Task QueueDeferredAsync(string reportPath)
        {
            await Task.Delay(2000);
            _channel.Writer.TryWrite(reportPath);
        }

        private static void Supersede(string reportId, string type, string message)
        {
            _supersedeId = reportId;
            _supersedeType = type;
            _supersedeMessage = message;
            _supersedeTime = MonotonicUnixTime.Now;
        }

        public static void TrackError(Exception ex)
        {
            ProcessException(ex);
        }

        public static void TrackError(string message)
        {
            ProcessException(NativeUtils.GetBackTrace("TrackErrorException", message));
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
                AppSettings.Diagnostics.LegacyScrollBars = true;
            }
            else if (args.Exception is NotSupportedException)
            {
                MitigateToolTipCrash();
                return;
            }

            if (AppSettings.Diagnostics.ShowMemoryUsage && Window.Current?.Content?.XamlRoot != null)
            {
                _ = MessagePopup.ShowAsync(WindowContext.Current.XamlRoot, args.Exception.ToString(), "Unhandled exception", "OK");
            }
        }

        /// <summary>
        /// UWP's ToolTip fast-fails with E_FAIL when its owner is torn down while the tooltip is
        /// still opening - reliably so when the owner is a Hyperlink. It arrives here as a
        /// NotSupportedException, and the app only survives if every visible tooltip is forced
        /// through close/open/close, which is what detaches it from the dead owner.
        ///
        /// Window.Current is deliberate: this runs from the app-wide exception handler, with no
        /// element to resolve a XamlRoot from.
        /// </summary>
        private static void MitigateToolTipCrash()
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
            // Only OnUnhandledExceptionDetected sets this, and only just now: the fail-fast hook
            // is reporting the crash that handler already wrote a thinner report for. Propagate
            // named the exception and the stowed records did not, so take its identity along
            // with its id and leave one report behind instead of two.
            var supersede = _supersedeId != null && MonotonicUnixTime.Now - _supersedeTime < 5
                ? _supersedeId
                : null;

            _supersedeId = null;

            if (supersede != null && !string.IsNullOrEmpty(_supersedeType))
            {
                error.Type = _supersedeType;
                error.Message = _supersedeMessage;
            }

            ProcessException(error, supersede);
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

            var count = AppSettings.Diagnostics.UpdateCount;

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
                var size = WindowContext.Current.Bounds;

                var ratio = AppSettings.DialogsWidthRatio;
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
                _currentTokens = AppSettings.ReportsCount;
                _lastRefill = AppSettings.ReportsDate;
                Refill();
            }

            private void SaveState()
            {
                AppSettings.ReportsCount = _currentTokens;
                AppSettings.ReportsDate = _lastRefill;
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
