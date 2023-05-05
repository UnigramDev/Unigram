//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Telegram.Services;

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
            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex);
#endif

            Log(LogLevel.Error, null, exception.ToString(), member, filePath, line);
        }

        public static void Info(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Info, null, message, member, filePath, line);
        }

        private static readonly List<string> _lastCalls = new();

        private static void Log(LogLevel level, Type type, string message, string member, string filePath, int line)
        {
            var limit = SettingsService.Current.Diagnostics.LoggerLimit;
            if (limit == 0)
            {
                return;
            }

            // We use UtcNow instead of Now because Now is expensive.
            string entry;
            if (message.Length > 0)
            {
                entry = string.Format(FormatWithMessage, DateTime.UtcNow, level, member, line, message);
            }
            else
            {
                entry = string.Format(FormatWithoutMessage, DateTime.UtcNow, level, member, line);
            }

            _lastCalls.Add(entry);

            if (_lastCalls.Count > limit)
            {
                _lastCalls.RemoveAt(0);
            }

            if (level != LogLevel.Debug || message.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine(entry);
            }
        }

        private const string FormatWithMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}] {4}";
        private const string FormatWithoutMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}]";

        public static string Dump()
        {
            if (SettingsService.Current.Diagnostics.LoggerLimit == 0)
            {
                return "Logs are disabled";
            }

            return string.Join('\n', _lastCalls);
        }
    }
}
