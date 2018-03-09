using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Unigram.Logs
{
	public sealed class Logger
	{
		private Logger()
		{
		}

        [Conditional("DEBUG")]
        public static void AddListener(ILogListener listener)
		{
			lock (_instance._listeners)
			{
                _instance._listeners.Add(listener);
			}
		}

        [Conditional("DEBUG")]
		public static void Critical(string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Critical, null, message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Critical(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Critical, sender.GetType(), message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Debug(string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Debug, null, message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Debug(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Debug, sender.GetType(), message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Error(string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Error, null, message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Error(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Error, sender.GetType(), message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Info(string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Info, null, message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Info(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Info, sender.GetType(), message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        private static void Log(LogLevel level, Type type, string message, string member, string filePath, int line)
		{
			lock (_instance._listeners)
			{
				if (_instance._listeners.Any())
				{
					foreach (ILogListener current in _instance._listeners)
					{
						if (type != null)
						{
							current.Log(level, type, message, member, filePath, line);
						}
						else
						{
							current.Log(level, message, member, filePath, line);
						}
					}
				}
			}
		}

        [Conditional("DEBUG")]
        public static void LogLocation(object sender, LogLevel level = LogLevel.Info, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(level, sender.GetType(), string.Empty, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void RemoveListener(ILogListener listener)
		{
			lock (_instance._listeners)
			{
                _instance._listeners.Remove(listener);
			}
		}

        [Conditional("DEBUG")]
        public static void Warning(string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Warning, null, message, member, filePath, line);
		}

        [Conditional("DEBUG")]
        public static void Warning(object sender, string message, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
		{
            Log(LogLevel.Warning, sender.GetType(), message, member, filePath, line);
		}

		private static readonly Logger _instance = new Logger();

		private readonly ICollection<ILogListener> _listeners = new List<ILogListener>();
	}
}
