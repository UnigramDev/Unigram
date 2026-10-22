//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Services
{
    public enum FileReadRank
    {
        Preload,
        Playback
    }

    /// <summary>
    /// A reader's claim on the download of a file it reads before it is complete.
    ///
    /// TDLib keeps one downloadFile request per file, and every call replaces its offset and
    /// limit, so two readers sending their own would take the download from each other on every
    /// read. Readers ask through <see cref="IClientService.RequestFileRead"/> instead, which sends
    /// only what the highest rank wants.
    /// </summary>
    public sealed class FileReadLease
    {
        private volatile bool _preempted;

        internal FileReadLease(int fileId, FileReadRank rank, Action preempted)
        {
            FileId = fileId;
            Rank = rank;
            Preempted = preempted;
        }

        public int FileId { get; }

        public FileReadRank Rank { get; }

        /// <summary>
        /// Whether a reader of a higher rank opened the file, after which nothing this lease asks
        /// for is sent. Set before the callback runs, and for a lease opened under one already,
        /// without it.
        /// </summary>
        public bool IsPreempted => _preempted;

        internal Action Preempted { get; }

        internal void Preempt()
        {
            _preempted = true;
        }

        // The rest is only touched under the downloads lock.
        internal bool IsClosed;

        internal bool HasWindow;
        internal int Priority;
        internal long Offset;
        internal long Limit;

        // When the window was last sent, as the window TDLib is running is the latest one.
        internal long Sequence;
    }
}
