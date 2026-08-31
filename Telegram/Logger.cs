//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Telegram.Native;
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

        public static void Error(object message, Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, message + "\n" + exception, member, filePath, line);
        }

        public static void Exception(Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            // The exception, not just Environment.StackTrace: that only says where the catch is,
            // which the caller attribution already gives. A caught exception logged without its
            // own message and stack tells you nothing about what went wrong.
            Log(LogLevel.Error, exception, member, filePath, line);

            if (Constants.RELEASE)
            {
                WatchDog.TrackError(exception);
            }
        }

        public static void Info(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Info, message, member, filePath, line);
        }

        // Ships with every crash report, so the size trades how much history a report
        // carries against how large every report gets.
        private const int TailCapacity = 200;

        private static readonly string[] _lastCalls = new string[TailCapacity];
        private static int _lastCallsHead;
        private static int _lastCallsCount;
        private static readonly object _lock = new();

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private static partial ulong GetTickCount64();
#else
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
#endif

        public static ulong TickCount => GetTickCount64();

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private unsafe static partial void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);
#else
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private unsafe static extern void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);
#endif

        static Logger()
        {
            NativeUtils.SetLogCallback(LogCallback);
        }

        private static void LogCallback(int level, string message, string member, string filePath, int line)
        {
            Log((LogLevel)level, message, member, filePath, line);
        }

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
                // Overwrite the oldest slot instead of shifting the window down, which
                // copied every retained entry on each call once the window was full.
                _lastCalls[_lastCallsHead] = entry;
                _lastCallsHead = (_lastCallsHead + 1) % TailCapacity;

                if (_lastCallsCount < TailCapacity)
                {
                    _lastCallsCount++;
                }
            }

            if ((int)level <= AppSettings.VerbosityLevel && (level != LogLevel.Debug || message != null))
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
            // We use UtcNow instead of Now because Now is expensive.
            long diff = 116444736000000000;
            long time = 0;

            GetSystemTimeAsFileTime(&time);

            var builder = new StringBuilder();

            lock (_lock)
            {
                // Once the window has wrapped, the slot due to be written next is the oldest.
                var start = _lastCallsCount < TailCapacity ? 0 : _lastCallsHead;

                for (int i = 0; i < _lastCallsCount; i++)
                {
                    builder.Append(_lastCalls[(start + i) % TailCapacity]);
                    builder.Append('\n');
                }
            }

            // Marks when the report was taken. Appended to the output rather than stored as
            // an entry, so that dumping neither evicts a line nor leaves a trail of markers
            // in the next dump.
            builder.AppendFormat("[{0:F3}] Bump", (time - diff) / 10_000_000d);
            return builder.ToString();
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
