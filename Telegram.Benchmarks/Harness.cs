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

namespace Telegram.Benchmarks
{
    /// <summary>
    /// A hand-rolled harness, because BenchmarkDotNet needs to spawn processes and emit IL and so
    /// cannot run under .NET Native. Both hosts run this same code over the same suite, which is
    /// the only way the desktop JIT numbers and the .NET Native numbers mean anything side by side.
    ///
    /// Reports the minimum rather than the mean: the fastest observed run is the one least polluted
    /// by whatever else the machine was doing.
    /// </summary>
    public sealed class Harness
    {
        private readonly List<Result> _results = new();
        private readonly List<string> _notes = new();

        public void Note(string note)
        {
            _notes.Add(note);
        }

        public readonly struct Result
        {
            public Result(string group, string name, double nanoseconds, long bytes)
            {
                Group = group;
                Name = name;
                Nanoseconds = nanoseconds;
                Bytes = bytes;
            }

            public string Group { get; }
            public string Name { get; }
            public double Nanoseconds { get; }
            public long Bytes { get; }
        }

        public IReadOnlyList<Result> Results => _results;

        public void Measure(string group, string name, Func<object?> action, int iterations = 0)
        {
            // One row that throws shouldn't cost the whole run - these take minutes on the UWP
            // hosts and are a pain to re-collect.
            try
            {
                MeasureCore(group, name, action, iterations);
            }
            catch (Exception ex)
            {
                _notes.Add($"{group} / {name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void MeasureCore(string group, string name, Func<object?> action, int iterations)
        {
            // Warm up, and let the AOT/JIT'd code settle before anything is recorded.
            for (int i = 0; i < 16; i++)
            {
                GC.KeepAlive(action());
            }

            if (iterations == 0)
            {
                // Aim for roughly 200ms of work per measurement, whatever the operation costs.
                var probe = Stopwatch.StartNew();
                var count = 0;
                while (probe.ElapsedMilliseconds < 20)
                {
                    GC.KeepAlive(action());
                    count++;
                }

                iterations = Math.Max(8, count * 10);
            }

            var best = double.MaxValue;

            for (int round = 0; round < 5; round++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var watch = Stopwatch.StartNew();

                for (int i = 0; i < iterations; i++)
                {
                    GC.KeepAlive(action());
                }

                watch.Stop();

                var per = watch.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
                if (per < best)
                {
                    best = per;
                }
            }

            _results.Add(new Result(group, name, best, MeasureAllocation(action)));
        }

        /// <summary>
        /// Allocation is measured on its own, separately from the timing rounds.
        /// </summary>
#if LEGACY_UWP
        // GC.GetAllocatedBytesForCurrentThread arrived in .NET Core 3.0 and isn't in the UWP
        // framework, so heap growth stands in for it. That only holds while no gen0 collection
        // runs mid-measurement - one would reclaim part of what was just allocated and the delta
        // would read low - so the run is retried with fewer iterations until none does.
        private static long MeasureAllocation(Func<object?> action)
        {
            foreach (var iterations in new[] { 1024, 256, 64, 16, 4, 4, 4, 4, 4, 4 })
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var collections = GC.CollectionCount(0);
                var before = GC.GetTotalMemory(false);

                for (int i = 0; i < iterations; i++)
                {
                    GC.KeepAlive(action());
                }

                var allocated = GC.GetTotalMemory(false) - before;

                if (GC.CollectionCount(0) == collections)
                {
                    return allocated / iterations;
                }
            }

            return -1; // never got a clean window
        }
#else
        private static long MeasureAllocation(Func<object?> action)
        {
            const int iterations = 64;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < iterations; i++)
            {
                GC.KeepAlive(action());
            }

            return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
        }
#endif

        public string Report()
        {
            var builder = new StringBuilder();
            var group = string.Empty;

            foreach (var result in _results)
            {
                if (result.Group != group)
                {
                    group = result.Group;
                    builder.AppendLine();
                    builder.AppendLine(group);
                    builder.AppendLine(new string('-', group.Length));
                }

                builder.AppendLine($"{result.Name,-34} {Format(result.Nanoseconds),12}  {result.Bytes,9:N0} B");
            }

            foreach (var note in _notes)
            {
                builder.AppendLine();
                builder.AppendLine(note);
            }

            return builder.ToString();
        }

        private static string Format(double nanoseconds)
        {
            if (nanoseconds >= 1_000_000)
            {
                return (nanoseconds / 1_000_000).ToString("N2") + " ms";
            }

            if (nanoseconds >= 1_000)
            {
                return (nanoseconds / 1_000).ToString("N2") + " us";
            }

            return nanoseconds.ToString("N1") + " ns";
        }
    }
}

