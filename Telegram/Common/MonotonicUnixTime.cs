//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Diagnostics;

namespace Telegram.Common
{
    public static class MonotonicUnixTime
    {
        private static readonly long startTicks = Stopwatch.GetTimestamp();
        private static readonly double startUnixTime = (DateTime.UtcNow -
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public static long Now
        {
            get
            {
                long ticks = Stopwatch.GetTimestamp();
                double elapsedSeconds = (double)(ticks - startTicks) / Stopwatch.Frequency;
                return (long)(startUnixTime + elapsedSeconds);
            }
        }
    }
}
