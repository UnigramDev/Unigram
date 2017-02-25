using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Telegram.Api.Helpers;
#if WIN_RT
using Windows.UI.Popups;
using Windows.UI.Core;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Services.Cache;
using System.Diagnostics;
#if WINDOWS_PHONE
using System.Threading;
using System.Windows;
#endif

namespace Telegram.Api.TL
{
    public enum LogSeverity
    {
        Error,
		Warning,
		Info
    }

    public static partial class TLUtils
    {
        private static void LogBugsenseError(string caption, Exception e)
        {
            var eventAggregator = TelegramEventAggregator.Instance;

            eventAggregator.Publish(new ExceptionInfo{ Caption = caption, Exception = e });
        }

        private static bool _isLogEnabled = false;

        public static bool IsLogEnabled
        {
            get { return _isLogEnabled; }
            set { _isLogEnabled = value; }
        }

        private static bool _isDebugEnabled = false;

        public static bool IsDebugEnabled
        {
            get { return _isDebugEnabled; }
            set { _isDebugEnabled = value; }
        }

        private static bool _isLongPollLogEnabled = false;

        public static bool IsLongPollDebugEnabled
        {
            get { return _isLongPollLogEnabled; }
            set { _isLongPollLogEnabled = value; }
        }
#if DEBUG
        private static bool _isPerformanceLogEnabled = true;
#else
        private static bool _isPerformanceLogEnabled = false;

#endif


        public static bool IsPerformanceLogEnabled
        {
            get { return _isPerformanceLogEnabled; }
            set { _isPerformanceLogEnabled = value; }
        }

        public static ObservableCollection<string> LongPollItems = new ObservableCollection<string>(); 

        public static ObservableCollection<string> PerformanceItems = new ObservableCollection<string>();

        public static ObservableCollection<string> DebugItems = new ObservableCollection<string>();

        public static ObservableCollection<string> LogItems = new ObservableCollection<string>();

        public static void WritePerformance(string str)
        {
            if (!IsPerformanceLogEnabled) return;

            Execute.BeginOnUIThread(() => PerformanceItems.Add(str));
        }

        public static void WriteLog(string str)
        {
            if (!IsLogEnabled) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Execute.BeginOnUIThread(() => LogItems.Add(timestamp + ": " + str));
        }

        public static void WriteLongPoll(string str)
        {
            if (!IsLongPollDebugEnabled) return;

            Execute.BeginOnUIThread(() => LongPollItems.Add(str));
        }

        public static void WriteLine(LogSeverity severity = LogSeverity.Info)
        {
            if (!IsDebugEnabled && severity != LogSeverity.Error) return;

            Execute.BeginOnUIThread(() => DebugItems.Add(" "));
        }

        public static void WriteLineAtBegin(string str, LogSeverity severity = LogSeverity.Info)
        {
            //if (!IsDebugEnabled && severity != LogSeverity.Error) return;

            Execute.BeginOnUIThread(() => DebugItems.Insert(0, str));
        }

        public static void WriteException(string caption, Exception e)
        {
            Execute.ShowDebugMessage(caption + Environment.NewLine + e);

#if LOG_REGISTRATION
            WriteLog(caption + Environment.NewLine + e);
#endif
            Logs.Log.Write(caption + Environment.NewLine + e);

            Execute.BeginOnUIThread(() =>
            {
                DebugItems.Add(caption + Environment.NewLine + e);
                LogBugsenseError(caption, e);
            });
        }

        public static void WriteException(Exception e)
        {
            WriteException(null, e);
        }

        public static void WriteLine(string str, LogSeverity severity = LogSeverity.Info)
        {
#if DEBUG
            if (!IsDebugEnabled && severity != LogSeverity.Error) return;

            Execute.BeginOnUIThread(() => DebugItems.Add(str));
#endif
        }

        public static string WriteThreadInfo()
        {
            var threadId =

#if WINDOWS_PHONE
                Thread.CurrentThread.ManagedThreadId;
#elif WIN_RT
                Environment.CurrentManagedThreadId;
#endif

            return "ThreadId " + threadId;
        }
    }
}
