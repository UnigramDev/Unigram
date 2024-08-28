//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram
{
    public sealed partial class Logger
    {
        public enum LogLevel
        {
            Assert,
            Error,
            Warning,
            Info,
            Debug,
        }

        public static void Assert(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Assert, message, member, filePath, line);
        }

        public static void Debug(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Debug, message, member, filePath, line);
        }

        public static void Warning(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Warning, message, member, filePath, line);
        }

        public static void Error(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, message, member, filePath, line);
        }

        public static void Error(Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, exception.ToString(), member, filePath, line);

#if !DEBUG
            var report = WatchDog.BuildReport(exception);
            //Microsoft.AppCenter.Crashes.Crashes.TrackError(exception, attachments: Microsoft.AppCenter.Crashes.ErrorAttachmentLog.AttachmentWithText(report, "crash.txt"));
#endif
        }

        public static void Info(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Info, message, member, filePath, line);
        }

        private static readonly List<string> _lastCalls = new();
        private static readonly object _lock = new();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static ulong TickCount => GetTickCount64();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private unsafe static extern void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);

        private static unsafe void Log(LogLevel level, object message, string member, string filePath, int line)
        {
            // We use UtcNow instead of Now because Now is expensive.
            long diff = 116444736000000000;
            long time = 0;

            GetSystemTimeAsFileTime(&time);

            string entry;
            if (message != null)
            {
                entry = string.Format(FormatWithMessage, (time - diff) / 10_000_000d, level, Path.GetFileName(filePath), line, member, message);
            }
            else
            {
                entry = string.Format(FormatWithoutMessage, (time - diff) / 10_000_000d, level, Path.GetFileName(filePath), line, member);
            }

            lock (_lock)
            {
                _lastCalls.Add(entry);

                if (_lastCalls.Count > 50)
                {
                    _lastCalls.RemoveAt(0);
                }
            }

            if ((int)level <= SettingsService.Current.VerbosityLevel && (level != LogLevel.Debug || message != null))
            {
                Client.Execute(new AddLogMessage(2, string.Format("[{0}:{1}][{2}] {3}", Path.GetFileName(filePath), line, member, message)));
            }

            if (level != LogLevel.Debug || message != null)
            {
                System.Diagnostics.Debug.WriteLine(entry);
            }
        }

        //private const string FormatWithMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}] {4}";
        //private const string FormatWithoutMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}]";

        private const string FormatWithMessage = "[{0:F3}][{2}:{3}][{4}] {5}";
        private const string FormatWithoutMessage = "[{0:F3}][{2}:{3}][{4}]";

        public static unsafe string Dump()
        {
            lock (_lock)
            {
                // We use UtcNow instead of Now because Now is expensive.
                long diff = 116444736000000000;
                long time = 0;

                GetSystemTimeAsFileTime(&time);

                _lastCalls.Add(string.Format("[{0:F3}] Bump", (time - diff) / 10_000_000d));
                return string.Join('\n', _lastCalls);
            }
        }
    }

    public partial class RuntimeException : Exception
    {
        public RuntimeException(Exception innerException)
            : base(innerException.Message, innerException)
        {

        }
    }
}
