//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

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
    }
}
