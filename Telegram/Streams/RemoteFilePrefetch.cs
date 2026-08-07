//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Streams
{
    /// <summary>
    /// Decides how far ahead of the read position to keep downloading, as the limit
    /// passed to TDLib's downloadFile.
    ///
    /// The target is <see cref="TargetSeconds"/> of media, extended when the connection
    /// cannot keep up with it.
    ///
    /// The limit is a stopping point rather than a request for bandwidth: TDLib downloads
    /// until it reaches it and then finishes with FILE_DOWNLOAD_LIMIT, so a window that
    /// is too small means repeatedly reaching that point and asking again. That costs the
    /// most on a slow link, where the reader is already close to starving, which is why a
    /// link slower than the media widens the window rather than narrowing it.
    ///
    /// Two properties come from how TDLib treats the limit rather than from taste.
    /// FileDownloader.update_downloaded_part cancels in-flight parts outside the window
    /// whenever the streaming offset moves, which is on every read, so the window is
    /// quantized to the part size and changes as rarely as it can. And shrinking is what
    /// causes those cancellations, so it only happens once a smaller target has held.
    /// </summary>
    public class RemoteFilePrefetch
    {
        // TDLib's part size settles between 64 KB and 512 KB depending on file size.
        // Rounding to the largest keeps the window on a part boundary at any of them, so
        // an estimate that drifts by a little moves no boundary and cancels no parts.
        private const long Quantum = 512 * 1024;

        private const long MinimumWindow = Quantum;

        // Not a TDLib limit: TDLib will read as far ahead as it is asked to. A sanity
        // bound on how far ahead of playback a single request may reach, which only binds
        // on high bitrate media, and most of all when the extension below applies.
        private const long MaximumWindow = 64 * 1024 * 1024;

        private const double TargetSeconds = 30;

        // Weight of each new measurement in the consumption rate.
        private const double Alpha = 0.2;

        // How long a smaller target must hold before the window follows it down. Long
        // enough that a dip in throughput does not cancel parts that are already coming.
        private const ulong ShrinkAfter = 5000;

        private readonly double _mediaBitsPerSecond;

        private double _consumedBitsPerSecond;

        private long _window;

        private long _shrinkTo;
        private ulong _shrinkSince;

        /// <param name="size">File size in bytes.</param>
        /// <param name="duration">Media duration in seconds, or zero when unknown.</param>
        public RemoteFilePrefetch(long size, double duration)
        {
            // The file's average bitrate: exact for the whole file, and only a starting
            // point. Advance corrects it with what the reader actually consumes, which is
            // what differs from the average on variable bitrate media.
            _mediaBitsPerSecond = duration > 0 && duration < 86400 && size > 0
                ? size * 8.0 / duration
                : 0;

            _window = Clamp(Target(_mediaBitsPerSecond, 0));
        }

        /// <summary>
        /// The window, in bytes, to pass as downloadFile's limit.
        /// </summary>
        public long Window => _window;

        /// <summary>
        /// Reports a completed read and the current download rate.
        /// </summary>
        /// <param name="count">Bytes the reader asked for.</param>
        /// <param name="elapsed">
        /// Time spent producing them, excluding any spent waiting for data. Measuring
        /// wall clock instead lets a stall deflate the rate, which shrinks the window,
        /// which causes the next stall.
        /// </param>
        /// <param name="downloadBitsPerSecond">
        /// Measured download rate, or zero while unknown.
        /// </param>
        public void Advance(long count, TimeSpan elapsed, double downloadBitsPerSecond)
        {
            if (count > 0 && elapsed > TimeSpan.Zero)
            {
                var observed = count * 8.0 / elapsed.TotalSeconds;

                _consumedBitsPerSecond = _consumedBitsPerSecond > 0
                    ? Alpha * observed + (1 - Alpha) * _consumedBitsPerSecond
                    : observed;
            }

            // The faster of the two: the file average covers the reader having barely
            // started, the measurement covers a section denser than the average.
            var media = Math.Max(_consumedBitsPerSecond, _mediaBitsPerSecond);

            Apply(Clamp(Target(media, downloadBitsPerSecond)));
        }

        private static long Target(double mediaBitsPerSecond, double downloadBitsPerSecond)
        {
            var target = mediaBitsPerSecond * TargetSeconds / 8.0;

            // Widened when the link cannot keep up with the media, so the download runs on
            // instead of reaching the limit and stopping every few seconds. It does not
            // make the bytes arrive sooner, but it stops the reader from paying the round
            // trip of asking again each time, which is worst exactly here.
            //
            // Bounded at twice, because past that the window is reaching so far ahead of
            // playback that most of it will never be watched.
            if (downloadBitsPerSecond > 0 && downloadBitsPerSecond < mediaBitsPerSecond)
            {
                target *= Math.Min(mediaBitsPerSecond / downloadBitsPerSecond, 2.0);
            }

            return (long)Math.Min(target, MaximumWindow);
        }

        private void Apply(long target)
        {
            if (target >= _window)
            {
                _window = target;

                _shrinkTo = 0;
                _shrinkSince = 0;

                return;
            }

            // Below the current window. Hold the largest target seen while waiting, so a
            // single dip does not drag the window further down than the period warrants.
            var now = Logger.TickCount;

            if (_shrinkSince == 0)
            {
                _shrinkTo = target;
                _shrinkSince = now;
            }
            else
            {
                _shrinkTo = Math.Max(_shrinkTo, target);
            }

            if (now - _shrinkSince >= ShrinkAfter)
            {
                _window = _shrinkTo;

                _shrinkTo = 0;
                _shrinkSince = 0;
            }
        }

        private static long Clamp(long window)
        {
            if (window <= MinimumWindow)
            {
                return MinimumWindow;
            }

            if (window >= MaximumWindow)
            {
                return MaximumWindow;
            }

            // Rounded up to a part boundary, so an estimate that moves by less than a
            // part leaves the window, and the set of protected parts, exactly as it was.
            return (window + Quantum - 1) / Quantum * Quantum;
        }
    }
}
