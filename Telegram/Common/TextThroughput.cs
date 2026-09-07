//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Diagnostics;
using Telegram.Services;

namespace Telegram.Common
{
    // What a text block costs in the running app: the call that gives it its text, and the two
    // layout passes. Both engines are counted the same way and from the same three places,
    // because the question is which of them is cheaper - and only one of them is alive in a
    // process, so the comparison is between two runs of the same chat rather than two columns.
    //
    // A runtime switch rather than [Conditional], for the reason TdThroughput gives: the build
    // whose numbers mean anything is the shipping one, which no debug define reaches. Off it
    // costs a static read per call, on it two timestamps.
    public static class TextThroughput
    {
        // Read at type init rather than assigned at startup: nothing here runs before the first
        // block is built anyway, and the setter keeps it in step from there.
        public static volatile bool Enabled = AppSettings.Diagnostics.MeasureTextLayout;

        // Written from every UI thread there is and read from one, without interlocks: a second
        // window can lose a call to a race, and a meter is not worth a locked bus cycle per
        // layout pass. The peak is what a single message did, which an average hides.
        public struct Counter
        {
            public long Calls;
            public long Ticks;
            public long Peak;

            public readonly double Seconds => Ticks / (double)Stopwatch.Frequency;

            public readonly double PeakSeconds => Peak / (double)Stopwatch.Frequency;
        }

        public static Counter DirectSetText;
        public static Counter DirectMeasure;
        public static Counter DirectArrange;

        public static Counter InlineSetText;
        public static Counter InlineMeasure;
        public static Counter InlineArrange;

        /// <summary>
        /// The timestamp to pass back to <see cref="Record"/>, or 0 when measuring is off -
        /// which is what makes Record a branch rather than a call.
        /// </summary>
        public static long Begin()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        public static void Record(ref Counter counter, long started)
        {
            if (started == 0)
            {
                return;
            }

            var elapsed = Stopwatch.GetTimestamp() - started;

            counter.Calls++;
            counter.Ticks += elapsed;

            if (elapsed > counter.Peak)
            {
                counter.Peak = elapsed;
            }
        }

        public static void Reset()
        {
            DirectSetText = default;
            DirectMeasure = default;
            DirectArrange = default;

            InlineSetText = default;
            InlineMeasure = default;
            InlineArrange = default;
        }
    }
}
