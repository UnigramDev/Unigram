using System;

namespace Unigram.Logs
{
    public static class LogHelper
    {
        private const string FormatWithoutType = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}:{3}] --- {4}";
        private const string FormatWithType = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}] [{1}] [{2}::{3}:{4}] --- {5}";

        public static string CreateEntryWithoutType(DateTime date, LogLevel level, string member, int line, string message)
        {
            return string.Format(FormatWithoutType, date, level, member, line, message);
        }

        public static string CreateEntryWithType(DateTime date, LogLevel level, string className, string member, int line, string message)
        {
            return string.Format(FormatWithType, date, level, className, member, line, message);
        }
    }
}
