//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Diagnostics;

namespace Telegram.Common
{
    // What the JSON path actually moves in the running app: payloads, bytes and time spent inside
    // Client.Receive's parse. Telegram.Benchmarks measures the same parse in a loop over a fixed
    // corpus, which says how fast it can go, not how much of it a session asks for.
    //
    // Runtime switch rather than [Conditional] like Instrumentation and Profiler, because the
    // question is about the shipping build: the .NET Native Release configuration is the only one
    // whose numbers mean anything here, and it is the one configuration that never defines
    // INSTRUMENTATION.
    //
    // Off it costs a static read per payload. On it costs two timestamps, and on the pointer path
    // a scan for the payload length - that reader stops at the terminator and never learns how far
    // it went. The scan is deliberately outside the interval being timed, so it slows ingestion
    // without flattering the rate it reports.
    public static class TdThroughput
    {
        // Written from the UI thread when the checkbox changes, read on the TDLib thread.
        public static volatile bool Enabled;

        // One writer each. Everything but the file-check pair is written by the TDLib thread that
        // Client.Run owns; those two are written by ClientService's verification drain, of which
        // only one runs at a time. Readers get whatever they get: this is a rate meter, and an
        // Interlocked per payload would be measuring the instrument.
        private static long _payloads;
        private static long _bytes;
        private static long _ticks;
        private static long _handlerTicks;
        private static long _fileChecks;
        private static long _fileCheckTicks;
        private static long _since = Stopwatch.GetTimestamp();

        /// <summary>
        /// The timestamp to pass back to <see cref="Record(long, int)"/>, or 0 when measuring is
        /// off - which is what makes Record a branch rather than a call.
        /// </summary>
        public static long Begin()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        public static void Record(long started, int bytes)
        {
            if (started == 0)
            {
                return;
            }

            _ticks += Stopwatch.GetTimestamp() - started;
            _bytes += bytes;
            _payloads++;
        }

        /// <summary>
        /// For the pointer path, where the length is not known: the scan happens here, after the
        /// interval has been closed.
        /// </summary>
        public static unsafe void Record(long started, byte* payload)
        {
            if (started == 0)
            {
                return;
            }

            var elapsed = Stopwatch.GetTimestamp() - started;

            var length = 0;
            while (payload[length] != 0)
            {
                length++;
            }

            _ticks += elapsed;
            _bytes += length;
            _payloads++;
        }

        /// <summary>
        /// Files do not just parse: ClientResultHandler is re-entered mid-payload to dedupe them,
        /// and that work - a dictionary, a FileExists syscall the first time an id is seen, and an
        /// EventAggregator publish - happens inside the interval Record measures. It is charged to
        /// the parser unless it is subtracted, and at startup a chat list is hundreds of files that
        /// have never been seen before.
        /// </summary>
        public static long BeginHandler()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        public static void RecordHandler(long started)
        {
            if (started != 0)
            {
                _handlerTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        /// <summary>
        /// The existence checks, which now run on ClientService's drain rather than the TDLib
        /// thread - so this time is no longer any part of Seconds, and is reported separately for
        /// that reason. Counted because it is I/O: no build makes it cheaper, and it was a third of
        /// the parse before it moved.
        /// </summary>
        public static void RecordFileCheck(long started)
        {
            if (started != 0)
            {
                _fileCheckTicks += Stopwatch.GetTimestamp() - started;
                _fileChecks++;
            }
        }

        public static long Payloads => _payloads;

        public static long Bytes => _bytes;

        /// <summary>
        /// Time inside Client.Receive's parse, file handling included - which is the honest number
        /// for what an update costs, and the wrong one for what the reader costs.
        /// </summary>
        public static double Seconds => _ticks / (double)Stopwatch.Frequency;

        /// <summary>The part of it that was file dedupe rather than reading JSON.</summary>
        public static double HandlerSeconds => _handlerTicks / (double)Stopwatch.Frequency;

        public static long FileChecks => _fileChecks;

        public static double FileCheckSeconds => _fileCheckTicks / (double)Stopwatch.Frequency;

        /// <summary>
        /// Time since the last reset. Parsing as a share of this is the number that says whether
        /// any of this matters: the corpus says how fast the parser can go, this says how much of
        /// it a session actually asks for.
        /// </summary>
        public static double WallSeconds => (Stopwatch.GetTimestamp() - _since) / (double)Stopwatch.Frequency;

        public static void Reset()
        {
            _payloads = 0;
            _bytes = 0;
            _ticks = 0;
            _handlerTicks = 0;
            _fileChecks = 0;
            _fileCheckTicks = 0;
            _since = Stopwatch.GetTimestamp();
        }
    }
}
