//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unigram.Logs
{
    public sealed class Logger
    {
        [Conditional("DEBUG")]
        public static void Critical(LogTarget tag, string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Critical, null, message, member, filePath, line);
        }

        [Conditional("DEBUG")]
        public static void Critical(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogTarget.None, LogLevel.Critical, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Critical(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Critical, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Debug(LogTarget tag, string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Debug, null, message, member, filePath, line);
        }

        [Conditional("DEBUG")]
        public static void Debug(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogTarget.None, LogLevel.Debug, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Debug(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Debug, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Error(LogTarget tag, string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Error, null, message, member, filePath, line);
        }

        [Conditional("DEBUG")]
        public static void Error(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogTarget.None, LogLevel.Error, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Error(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Error, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Info(LogTarget tag, string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Info, null, message, member, filePath, line);
        }

        [Conditional("DEBUG")]
        public static void Info(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogTarget.None, LogLevel.Info, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Info(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Info, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        private static void Log(LogTarget tag, LogLevel level, Type type, string message, string member, string filePath, int line)
        {
            //Logs.Log.Write(LogHelper.CreateEntryWithoutType(DateTime.Now, level, member, line, message));
            System.Diagnostics.Debug.WriteLine(LogHelper.CreateEntryWithoutType(DateTime.Now, level, member, line, message));
        }

        //[Conditional("DEBUG")]
        //public static void LogLocation(object sender, LogLevel level = LogLevel.Info, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(level, sender.GetType(), string.Empty, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Warning(LogTarget tag, string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Warning, null, message, member, filePath, line);
        }

        [Conditional("DEBUG")]
        public static void Warning(string message = "", [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogTarget.None, LogLevel.Warning, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Warning(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Warning, sender.GetType(), message, member, filePath, line);
        //}
    }

    [Flags]
    public enum LogTarget
    {
        None,

        Lifecycle,
        API,
        Chat,
        Notifications,
        Contacts,
        Recording,
        BootStrapper
    }
}
