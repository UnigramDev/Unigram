//
// Copyright Fela Ameghino 2015-2023
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
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public sealed class Logger
    {
        public static void Critical(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Critical, null, message, member, filePath, line);
        }

        public static void Debug(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Debug, null, message, member, filePath, line);
        }

        public static void Warning(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Warning, null, message, member, filePath, line);
        }

        public static void Error(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, null, message, member, filePath, line);
        }

        public static void Error(Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
#if !DEBUG
            Microsoft.AppCenter.Crashes.Crashes.TrackError(exception, attachments: Microsoft.AppCenter.Crashes.ErrorAttachmentLog.AttachmentWithText(Dump(), "crash.txt"));
#endif

            Log(LogLevel.Error, null, exception.ToString(), member, filePath, line);
        }

        public static void Info(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Info, null, message, member, filePath, line);
        }

        private static readonly List<string> _lastCalls = new();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private unsafe static extern void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);

        private static unsafe void Log(LogLevel level, Type type, string message, string member, string filePath, int line)
        {
            // We use UtcNow instead of Now because Now is expensive.
            long diff = 116444736000000000;
            long time = 0;

            GetSystemTimeAsFileTime(&time);

            string entry;
            if (message.Length > 0)
            {
                entry = string.Format(FormatWithMessage, (time - diff) / 10_000_000d, level, filePath, line, member, message);
            }
            else
            {
                entry = string.Format(FormatWithoutMessage, (time - diff) / 10_000_000d, level, filePath, line, member);
            }

            _lastCalls.Add(entry);

            if (_lastCalls.Count > 50)
            {
                _lastCalls.RemoveAt(0);
            }

            if (SettingsService.Current.Diagnostics.LoggerSink && (level != LogLevel.Debug || message.Length > 0))
            {
                Client.Execute(new AddLogMessage(2, string.Format("[{0}:{1}][{2}] {3}", level, Path.GetFileName(filePath), line, member, message)));
                System.Diagnostics.Debug.WriteLine(entry);
            }
        }

        //private const string FormatWithMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}] {4}";
        //private const string FormatWithoutMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}]";

        private const string FormatWithMessage = "[{0:F3}][{2}:{3}][{4}] {5}";
        private const string FormatWithoutMessage = "[{0:F3}][{2}:{3}][{4}]";

        public static string Dump()
        {
            return string.Join('\n', _lastCalls);
        }
    }
}
