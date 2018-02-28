using System;

namespace Telegram.Logs
{
	public static class LogHelper
	{
		public static string CreateEntryWithoutType(DateTime date, LogLevel level, string member, int line, string message)
		{
			return string.Format("[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}:{3}] --- {4}", new object[]
			{
				date,
				level,
				member,
				line,
				message
			});
		}

		public static string CreateEntryWithType(DateTime date, LogLevel level, string className, string member, int line, string message)
		{
			return string.Format("[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}::{3}:{4}] --- {5}", new object[]
			{
				date,
				level,
				className,
				member,
				line,
				message
			});
		}

		private const string FormatWithoutType = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}:{3}] --- {4}";

		private const string FormatWithType = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}::{3}:{4}] --- {5}";
	}
}
