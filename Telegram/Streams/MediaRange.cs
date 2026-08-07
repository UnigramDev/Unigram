//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Net.Http.Headers;

namespace Telegram.Streams
{
    /// <summary>
    /// The part of a file a media request asks for, resolved against the size of that file.
    ///
    /// Answering a byte range out of a file that is still downloading takes two numbers,
    /// and they are not the same one: which bytes to return now, and how far past them to
    /// keep downloading so the next request is already answered when it arrives.
    /// </summary>
    public readonly struct MediaRange
    {
        // An open ended request asks for the rest of the file, which the player is not
        // going to wait for. It gets a piece, and asks again from where the piece ended.
        private const long ChunkSize = 64 * 1024;

        // Ceiling on one response. The whole remainder of the file is otherwise a valid
        // answer, and producing it means holding all of it in memory at once.
        private const long MaximumCount = 16 * 1024 * 1024;

        // Where the metadata of a file that was not laid out for streaming sits.
        private const double TrailerStart = 0.95;

        private const double WindowSeconds = 15;
        private const long DefaultWindow = 1 * 1024 * 1024;
        private const long MaximumWindow = 4 * 1024 * 1024;

        private MediaRange(long offset, long count, long window)
        {
            Offset = offset;
            Count = count;
            Window = window;
        }

        /// <summary>
        /// Offset of the first byte to return.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Number of bytes to return. Zero when there is nothing to answer with, which is
        /// an empty file or a range starting past the end of one.
        /// </summary>
        public long Count { get; }

        /// <summary>
        /// How far past <see cref="Offset"/> to keep downloading, as downloadFile's limit.
        ///
        /// Deliberately not <see cref="Count"/>. A response is bounded by what fits in
        /// memory, while the download is bounded by what the player is about to come back
        /// for, and in the tail of a file those are far apart. The two only coincide where
        /// the request named both of its ends, since reading past what was asked for is
        /// then a guess.
        /// </summary>
        public long Window { get; }

        /// <summary>
        /// Offset of the last byte to return, for Content-Range.
        ///
        /// The response stays a 206 even where nothing asked for a range, since a request
        /// for the whole file is answered a piece at a time as well. Reporting the piece
        /// as a whole body would have the player believe it had reached the end.
        /// </summary>
        public long To => Offset + Count - 1;

        /// <param name="header">Range header, or null when the request carried none.</param>
        /// <param name="size">File size in bytes.</param>
        /// <param name="duration">Media duration in seconds, or zero when unknown.</param>
        public static MediaRange Parse(string header, long size, double duration)
        {
            if (size <= 0)
            {
                return default;
            }

            if (header != null && RangeHeaderValue.TryParse(header, out var ranges))
            {
                // Only the first range is answered. A request may carry several, and the
                // answer to one that does is a multipart response, which no media player
                // asks for and nothing here can produce.
                foreach (var part in ranges.Ranges)
                {
                    return Resolve(part, size, duration);
                }
            }

            return Whole(size);
        }

        private static MediaRange Resolve(RangeItemHeaderValue part, long size, double duration)
        {
            // bytes=-n, the last n bytes. From is empty and To carries a length rather
            // than a position, so reading it as one starts the response in the wrong
            // place and answers with bytes that were never asked for.
            if (part.From == null)
            {
                var length = Math.Clamp(part.To ?? size, 0, size);
                var offset = size - length;

                return new MediaRange(offset, Math.Min(length, MaximumCount), length);
            }

            if (part.From >= size)
            {
                return default;
            }

            var from = Math.Max(part.From.Value, 0);

            // bytes=a-b, both ends named. Exactly what was asked for, clamped to the file
            // in case the last byte is past the end of it.
            if (part.To.HasValue)
            {
                var last = Math.Min(part.To.Value, size - 1);
                var count = Math.Clamp(last - from + 1, 0, MaximumCount);

                return new MediaRange(from, count, count);
            }

            var remaining = size - from;

            // bytes=a- landing in the tail of the file, where the metadata of something
            // not laid out for streaming sits. Playback cannot start until all of it has
            // been read, and the player does not read it in one request: it comes back
            // for piece after piece of the tail before the first frame appears.
            //
            // So the window covers the whole tail even though the response does not.
            // Downloading all of it once means those later requests are answered out of
            // what has already arrived, instead of each one waiting on TDLib again.
            if ((double)from / size >= TrailerStart)
            {
                return new MediaRange(from, Math.Min(remaining, MaximumCount), remaining);
            }

            return new MediaRange(from, Math.Min(remaining, ChunkSize), WindowFor(size, duration));
        }

        private static MediaRange Whole(long size)
        {
            // Nothing asked for a range, so the whole file is wanted and all of it is
            // worth downloading. Only the response is held to one piece of it.
            return new MediaRange(0, Math.Min(size, MaximumCount), size);
        }

        /// <summary>
        /// How far to read past an open ended request: enough media to cover the wait for
        /// the next one, off the file's average bitrate.
        /// </summary>
        private static long WindowFor(long size, double duration)
        {
            if (duration > 0)
            {
                return Math.Clamp((long)(size / duration * WindowSeconds), ChunkSize, MaximumWindow);
            }

            return DefaultWindow;
        }
    }
}
