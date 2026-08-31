//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Telegram.Common
{
    // Scoped timings, written to the debug output through Logger.Info.
    //
    // Begin/End are [Conditional("INSTRUMENTATION")] like Instrumentation.Register, so a probe can
    // stay where it was needed and compiles out of every build that doesn't define the symbol. That
    // is the whole point of the shape: a helper returning a timestamp would leave the reading itself
    // in the hot path, which is exactly where these get placed.
    //
    // Scopes nest and are reported indented, so an outer scope shows what it contains. End unwinds
    // to its own label, so a return between a pair reports the abandoned scopes rather than silently
    // charging the rest of the run to them.
    //
    // Tally closes a scope into a running total instead of logging it, for paths too short and too
    // frequent to report one by one — where a line per call would cost more than the call.
    public static class Profiler
    {
        // Under this is noise: the paths worth probing run on every keystroke.
        private const double ThresholdMs = 1;

        [ThreadStatic]
        private static Stack<(string Label, long Timestamp)> t_scopes;

        [Conditional("INSTRUMENTATION")]
        public static void Begin(string label)
        {
            (t_scopes ??= new()).Push((label, Stopwatch.GetTimestamp()));
        }

        [Conditional("INSTRUMENTATION")]
        public static void End(string label)
        {
            while (t_scopes != null && t_scopes.Count > 0)
            {
                var scope = t_scopes.Pop();
                var elapsed = (Stopwatch.GetTimestamp() - scope.Timestamp) * 1000d / Stopwatch.Frequency;

                if (elapsed >= ThresholdMs)
                {
                    Logger.Info(new string(' ', t_scopes.Count * 2) + scope.Label + " " + elapsed.ToString("F1") + "ms");
                }

                if (scope.Label == label)
                {
                    break;
                }
            }
        }

        // --- tally --------------------------------------------------------------

        // Reports at 1, 10, 100 and 1000 samples and every 1000 after that. The first sample says
        // the probe is live, which is otherwise indistinguishable from a path that never runs, and
        // by the time the interval settles the means have too — cumulative and never reset, which
        // is what makes two recordings comparable without putting the same content on screen twice.
        private const int ReportEvery = 1000;

        private sealed class Counter
        {
            public long Calls;
            public long Ticks;
            public long Min = long.MaxValue;
            public long Max;
        }

        // Shared across windows (a second window is a second UI thread), unlike the scope stack.
        private static readonly Dictionary<string, Counter> s_counters = new();
        private static readonly object s_lock = new();
        private static long s_samples;
        private static long s_next = 1;

        /// <summary>Closes a scope opened by <see cref="Begin"/> into the running total.</summary>
        [Conditional("INSTRUMENTATION")]
        public static void Tally(string label)
        {
            var now = Stopwatch.GetTimestamp();

            while (t_scopes != null && t_scopes.Count > 0)
            {
                var scope = t_scopes.Pop();
                Accumulate(scope.Label, now - scope.Timestamp);

                if (scope.Label == label)
                {
                    break;
                }
            }
        }

        /// <summary>Counts an event that has no duration, reported as a row with calls only.</summary>
        [Conditional("INSTRUMENTATION")]
        public static void Count(string label)
        {
            Accumulate(label, 0);
        }

        /// <summary>
        /// Logs the table on demand. The automatic report needs another 1000 samples between
        /// prints, which is a long recording for a probe that fires once or twice per item, so a
        /// short one would otherwise end before saying anything. Cumulative like the rest:
        /// printing does not reset, and does not disturb the automatic interval either.
        /// </summary>
        [Conditional("INSTRUMENTATION")]
        public static void Report()
        {
            string report;

            lock (s_lock)
            {
                report = Format();
            }

            Logger.Info(report);
        }

        // --- contention ---------------------------------------------------------

        // For a scope that queues on something this class cannot see - a native lock, one device.
        // The label is split into .solo and .queued by whether anything else was already inside it,
        // and those two rows are what answer the question: what the scope costs uncontended, and
        // what it costs having waited. Min against mean on a single label cannot answer it, because
        // a sample delayed by a GC pause and one delayed by the lock look identical.
        //
        // Its own timestamp rather than the scope stack, because the pair brackets one synchronous
        // call and must close the label it opened - Tally unwinds by name, and the name here is not
        // known until entry. One counter for every label, so two contended scopes measured at once
        // would report each other's overlap: measure one at a time.
        private static int s_concurrent;

        [ThreadStatic]
        private static (string Label, long Timestamp) t_concurrent;

        [Conditional("INSTRUMENTATION")]
        public static void BeginConcurrent(string label)
        {
            var concurrent = Interlocked.Increment(ref s_concurrent);
            t_concurrent = (label + (concurrent > 1 ? ".queued" : ".solo"), Stopwatch.GetTimestamp());
        }

        /// <summary>Closes <see cref="BeginConcurrent"/>. Call it from a finally: an escaping
        /// exception would otherwise leave the counter high and report every later sample as
        /// queued.</summary>
        [Conditional("INSTRUMENTATION")]
        public static void TallyConcurrent()
        {
            Interlocked.Decrement(ref s_concurrent);

            if (t_concurrent.Label != null)
            {
                Accumulate(t_concurrent.Label, Stopwatch.GetTimestamp() - t_concurrent.Timestamp);
                t_concurrent = default;
            }
        }

        private static void Accumulate(string label, long ticks)
        {
            string report = null;

            lock (s_lock)
            {
                if (!s_counters.TryGetValue(label, out var counter))
                {
                    s_counters[label] = counter = new Counter();
                }

                counter.Calls++;
                counter.Ticks += ticks;

                if (ticks < counter.Min) counter.Min = ticks;
                if (ticks > counter.Max) counter.Max = ticks;

                if (++s_samples >= s_next)
                {
                    s_next = s_next < ReportEvery ? s_next * 10 : s_next + ReportEvery;
                    report = Format();
                }
            }

            if (report != null)
            {
                Logger.Info(report);
            }
        }

        // Calls, mean, min, max, total, and each total against the biggest one — which is the
        // outermost scope being tallied, so that last column is the share an inner path is worth
        // optimizing out of.
        //
        // Read the MIN, not the mean, to compare two recordings: one GC pause inside a scope with a
        // few hundred samples moves its mean by more than any change we are measuring, and it moves
        // the containing scope's with it. The min is what the path costs with nothing interfering,
        // and max says how badly that particular label was disturbed. Totals and counts only mean
        // something within one recording, since they follow whatever happened to be on screen.
        private static string Format()
        {
            var reference = 1L;

            foreach (var counter in s_counters.Values)
            {
                if (counter.Ticks > reference)
                {
                    reference = counter.Ticks;
                }
            }

            var builder = new StringBuilder("profiler ").Append(s_samples).Append(" samples");

            foreach (var pair in s_counters)
            {
                var counter = pair.Value;

                builder.Append("\n  ");
                builder.Append(pair.Key.PadRight(18));
                builder.Append(counter.Calls.ToString().PadLeft(7));
                builder.Append(" calls ");
                builder.Append((counter.Ticks * 1000000d / Stopwatch.Frequency / counter.Calls).ToString("F1").PadLeft(9));
                builder.Append("us mean ");
                builder.Append((counter.Min * 1000000d / Stopwatch.Frequency).ToString("F1").PadLeft(8));
                builder.Append("us min ");
                builder.Append((counter.Max * 1000000d / Stopwatch.Frequency).ToString("F1").PadLeft(9));
                builder.Append("us max ");
                builder.Append((counter.Ticks * 1000d / Stopwatch.Frequency).ToString("F1").PadLeft(9));
                builder.Append("ms ");
                builder.Append((counter.Ticks * 100d / reference).ToString("F0").PadLeft(4));
                builder.Append('%');
            }

            return builder.ToString();
        }
    }
}
