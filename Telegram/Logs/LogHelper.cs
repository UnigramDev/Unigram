//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Logs
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
