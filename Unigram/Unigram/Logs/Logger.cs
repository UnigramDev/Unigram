using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unigram.Logs
{
    public sealed class Logger
    {
        [Conditional("DEBUG")]
        public static void Critical(Target tag, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Critical, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Critical(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Critical, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Debug(Target tag, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Debug, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Debug(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Debug, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Error(Target tag, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Error, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Error(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Error, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        public static void Info(Target tag, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Info, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Info(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Info, sender.GetType(), message, member, filePath, line);
        //}

        [Conditional("DEBUG")]
        private static void Log(Target tag, LogLevel level, Type type, string message, string member, string filePath, int line)
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
        public static void Warning(Target tag, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(tag, LogLevel.Warning, null, message, member, filePath, line);
        }

        //[Conditional("DEBUG")]
        //public static void Warning(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        //{
        //    Log(LogLevel.Warning, sender.GetType(), message, member, filePath, line);
        //}
    }

    [Flags]
    public enum Target
    {
        Lifecycle,
        API,
        Chat,
        Notifications,
        Contacts,
        Recording
    }
}
