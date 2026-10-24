//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class RemoteFileSource : AnimatedImageSource, IAsyncMediaPlayerSource
    {
        // Slim, because UpdateFile can run after the handle of a ManualResetEvent was finalized:
        // a source dropped without Close stays in UpdateManager's ConditionalWeakTable until the
        // GC after the one that finalized it. Only ReadCallbackAsync materializes a kernel handle,
        // and its caller always closes.
        private readonly ManualResetEventSlim _event;
        private readonly object _stateLock = new();

        private readonly IClientService _clientService;
        private readonly File _file;
        private readonly int _priority;
        private readonly bool _adaptive;
        private readonly FileReadRank _rank;

        private readonly RemoteFileBitrate _bitrate;

        // Null unless this source owns the download, in which case ReadCallback uses the
        // window it is handed rather than one worked out here.
        private readonly RemoteFilePrefetch _prefetch;

        private readonly bool _streaming;

        private long _offset;
        private long _count;

        // Where the last window asked for ends, which RemoteFileBitrate measures against. Written
        // by the reader, read on the thread updates arrive on.
        private long _requestedEnd;

        private bool _closed;

        // Null while closed. Under _stateLock.
        private FileReadLease _lease;

        private long _fileToken;

        /// <param name="streaming">
        /// Whether this source is the reader that owns the file's download, playing it
        /// forwards from beginning to end. It then sizes the download window from the rate
        /// it consumes at, rather than using the one passed to <see cref="ReadCallback"/>,
        /// and stops the download when it closes.
        ///
        /// Callers that serve one ranged request each want this off: they have nothing to
        /// measure a rate from, and the file is being read by others at the same time.
        /// </param>
        /// <param name="rank">
        /// A preload reads behind everything the chat shows, and closes as soon as playback
        /// opens the file, which then downloads what the preload was waiting for.
        /// </param>
        public RemoteFileSource(IClientService clientService, File file, double duration, bool streaming = true, FileReadRank rank = FileReadRank.Playback)
        {
            _event = new ManualResetEventSlim(false);

            _clientService = clientService;
            _file = file;
            _priority = rank == FileReadRank.Preload ? 1 : 32;
            _adaptive = true;
            _rank = rank;

            _bitrate = new RemoteFileBitrate(file);

            _streaming = streaming;

            if (streaming)
            {
                _prefetch = new RemoteFilePrefetch(file.Size, duration);
            }

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
        }

        public RemoteFileSource(IClientService clientService, File file/*, int priority = 32*/)
        {
            _event = new ManualResetEventSlim(false);

            _clientService = clientService;
            _file = file;
            _priority = 32;
            _adaptive = false;
            _rank = FileReadRank.Playback;

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
        }

        public override void SeekCallback(long offset)
        {
            lock (_stateLock)
            {
                // Nothing here touches the prefetch window. Seeking moves where the reader
                // is, not how fast the link runs or how dense the media is, and both of
                // those are what size the window.
                _offset = offset;

                if (_file.Local.CanBeDownloaded && !_file.Local.IsDownloadingCompleted && !_adaptive)
                {
                    Download(offset, 0);
                }
            }
        }

        private bool AtEndOfStream => _closed || _offset >= _file.Size - 1;

        /// <summary>
        /// Whether reporting zero bytes would mean anything other than the end.
        ///
        /// Zero is the caller's end of stream: the player and VideoAnimation both take a
        /// zero byte count for the end of the media. It may therefore only be reported once
        /// there genuinely is nothing left; every other "nothing available yet" has to keep
        /// waiting instead.
        ///
        /// This is also what terminates the read loops. A zero count would satisfy
        /// MustWait without waiting and read back zero bytes, which is neither progress
        /// nor the end.
        /// </summary>
        private bool CanRead(long count)
        {
            return count > 0 && !AtEndOfStream;
        }

        /// <summary>
        /// How long one wait may last before the range is asked for again. An update that
        /// never arrives - a DownloadFile TDLib answered with an error, a download cancelled
        /// from somewhere else - would otherwise never be noticed.
        /// </summary>
        private const int ReadRetryInterval = 5 * 1000;

        /// <summary>
        /// How long the whole read may take before it gives up.
        ///
        /// A read that never returns would hold its reader's thread for good. Long enough
        /// that a slow link still plays: only one delivering nothing at all for this long
        /// gives up.
        /// </summary>
        private const ulong ReadTimeout = 30 * 1000;

        public override void ReadCallback(long count, long buffer, out long bytesRead)
        {
            var started = Logger.TickCount;
            var waited = 0UL;

            while (true)
            {
                if (MustWait(count, PrefetchWindow(buffer)))
                {
                    var blocked = Logger.TickCount;
                    _event.Wait(ReadRetryInterval);
                    waited += Logger.TickCount - blocked;
                }

                bytesRead = DownloadedBytes;

                if (bytesRead != 0 || !CanRead(count))
                {
                    // Time spent blocked is subtracted: the reader consumed nothing then,
                    // and counting it would deflate the rate exactly when a stall makes a
                    // larger window the thing that would have helped.
                    //
                    // Both are unsigned, so the subtraction is floored rather than allowed
                    // to wrap, in case the clock is read either side of an adjustment.
                    var elapsed = Logger.TickCount - started;

                    _prefetch?.Advance(count,
                        TimeSpan.FromMilliseconds(elapsed > waited ? elapsed - waited : 0),
                        DownloadBitsPerSecond);
                    return;
                }

                if (Logger.TickCount - started >= ReadTimeout)
                {
                    // Reported as an error rather than as zero: zero is the end of the media
                    // to a reader, which would truncate the file without saying so.
                    Logger.Warning($"Nothing for {_file.Id} in {Logger.TickCount - started}ms, offset: {_offset}, count: {count}");

                    bytesRead = -1;
                    return;
                }

                // Nothing yet, and not the end: MustWait requested the range again on
                // the way through, so go back and wait for the update that answers it.
            }
        }

        // A window the caller asks for wins. The prefetch policy measures the rate the reader
        // consumes at, which describes a reader that reads at the media's pace; one that keeps its
        // own read-ahead reads in bursts, and the bursts read as an enormous bitrate.
        private long PrefetchWindow(long buffer)
        {
            return buffer > 0 ? buffer : _prefetch?.Window ?? buffer;
        }

        public async Task<long> ReadCallbackAsync(long count, long buffer)
        {
            while (true)
            {
                if (MustWait(count, PrefetchWindow(buffer)))
                {
                    await _event.WaitHandle.WaitOneAsync();
                }

                var bytesRead = DownloadedBytes;

                if (bytesRead != 0 || !CanRead(count))
                {
                    return bytesRead;
                }
            }
        }

        // RemoteFileBitrate divides by a millisecond tick count, so what it reports is
        // bits per millisecond. It used to be handed to the native player as DownloadRate
        // and compared there against a per-second media bitrate, which made the link look
        // a thousand times slower than it is.
        private double DownloadBitsPerSecond => (_bitrate?.CurrentBitrate ?? 0) * 1000;

        public double DownloadBytesPerSecond => DownloadBitsPerSecond / 8;

        private long DownloadedBytes => CalculateDownloadedBytes();

        private long CalculateDownloadedBytes()
        {
            if (_closed)
            {
                return -1;
            }

            if (_offset >= _file.Size - 1)
            {
                return 0;
            }

            // Tested before the window, not inside it: once the file is complete every
            // offset is available, whatever download_offset happens to have been left
            // at. Inside the window this returned 0, which ReadCallback would now spin
            // on, since MustWait does not wait for a file that is already downloaded.
            if (_file.Local.IsDownloadingCompleted && _file.Local.Path.Length > 0)
            {
                return Math.Max(0, _file.Size - _offset);
            }

            var begin = _file.Local.DownloadOffset;
            var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

            var inBegin = _offset >= begin;
            var inEnd = end >= _offset;

            if (_file.Local.Path.Length > 0 && inBegin && inEnd)
            {
                return Math.Max(0, end - _offset);
            }

            // Outside the window the last update described, so nothing is known yet.
            //
            // This used to send GetFileDownloadedPrefixSize and block for up to half a
            // second on the answer, which could not arrive: Client.Run dispatches
            // updates and responses on one thread, MustWait holds _stateLock across the
            // wait, and UpdateFile needs that same lock, so the reply queued behind a
            // dispatch this call was itself blocking. It timed out and reported zero.
            //
            // The query is also redundant. MustWait calls DownloadFile with the current
            // offset on every pass, and TDLib sets download_offset to it and recomputes
            // downloaded_prefix_size against it, so the next update carries the answer.
            return 0;
        }

        protected bool MustWait(long count, long buffer)
        {
            lock (_stateLock)
            {
                if (_closed || _file.Local.IsDownloadingCompleted || _offset >= _file.Size - 1)
                {
                    return false;
                }

                count = Math.Min(_file.Size - _offset, count);
                buffer = _adaptive ? Math.Min(_file.Size - _offset, Math.Max(count, buffer)) : 0;

                var downloaded = CalculateDownloadedBytes();
                if (downloaded >= count)
                {
                    // Always request new bytes
                    Download(_offset, buffer);

                    //Logger.Debug($"Next chunk is available for {_file.Id}, offset: {_offset}, limit: {buffer}, count: {count}, download: {_file.Local.DownloadOffset}, prefix: {_file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                    return false;
                }

                // Reset event before requesting download to avoid race condition
                _event.Reset();
                _count = count;

                Download(_offset, buffer);

                //Logger.Debug($"Not enough data available for {_file.Id}, offset: {_offset}, limit: {buffer}, count: {count}, download: {_file.Local.DownloadOffset}, prefix: {_file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                return true;
            }
        }

        private void Download(long offset, long limit)
        {
            if (_lease == null)
            {
                return;
            }

            Interlocked.Exchange(ref _requestedEnd, limit > 0 ? offset + limit : _file.Size);
            _clientService.RequestFileRead(_lease, _priority, offset, limit);
        }

        public override string FilePath => _file.Local.Path;
        public override long FileSize => _file.Size;

        public override long Id => _file.Id;

        public override long Offset => _offset;

        private void UpdateFile(File file)
        {
            if (file.Id != _file.Id)
            {
                return;
            }

            _bitrate?.Update(file, Interlocked.Read(ref _requestedEnd));

            lock (_stateLock)
            {
                // No need to process the update if no one is waiting
                if (_event.IsSet)
                {
                    return;
                }

                var begin = _file.Local.DownloadOffset;
                var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

                var inBegin = _offset >= begin;
                var inEnd = end >= _offset + _count /*|| end == _file.Size*/;

                var available = _file.Local.Path.Length > 0 && ((inBegin && inEnd) || _file.Local.IsDownloadingCompleted);
                var canceled = _closed || !file.Local.IsDownloadingActive;

                if (available || canceled)
                {
                    //if (available)
                    //{
                    //    Logger.Debug($"Next chunk is available for {_file.Id}, offset: {_offset}, count: {_count}, download: {_file.Local.DownloadOffset}, prefix: {file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                    //}
                    //else
                    //{
                    //    Logger.Info($"Download was canceled for {_file.Id}");
                    //}

                    _event.Set();
                }
            }
        }

        public void Open()
        {
            bool outranked;

            lock (_stateLock)
            {
                _closed = false;

                // Not taken in the constructor: a source can be created and dropped without ever
                // being read, and only Close gives a lease back. Readers open the source before
                // reading, and again to replay it, having closed it at the end.
                _lease ??= _clientService.OpenFileRead(_file.Id, _rank, _rank == FileReadRank.Preload ? Close : null);

                // Nothing a preload asks for is sent while the file plays, so its reads would
                // only wait out their timeout.
                outranked = _rank == FileReadRank.Preload && _lease.IsPreempted;
            }

            if (outranked)
            {
                Close();
                return;
            }

            // Close unsubscribed. Without this the reopened source hears no updateFile, so
            // _event is never set again and the first read that has to wait blocks until it
            // times out, every time. Outside _stateLock, which UpdateFile takes on the
            // thread updates are published from.
            UpdateManager.Subscribe(this, _clientService, _file, ref _fileToken, UpdateFile);

            SeekCallback(0);
        }

        public void Close(/*bool cancel*/)
        {
            lock (_stateLock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;

                //Logger.Debug($"Disposing the stream");
                UpdateManager.Unsubscribe(this, ref _fileToken);

                // Only the reader that owns the download stops it, and only once no other
                // reader is left: the last window it asked for would otherwise keep fetching
                // ahead of a video nobody is watching. Sources serving one ranged request each
                // leave it to whoever else is reading the file.
                _clientService.CloseFileRead(_lease, _streaming);
                _lease = null;

                _event.Set();
            }

            //_event.Dispose();
            //_readLock.Dispose();
        }

        public class RemoteFileBitrate
        {
            public double CurrentBitrate => _bitrate;

            private readonly double _alpha = 0.2;

            private ulong _lastUpdateTime;
            private long _lastDownloadOffset;
            private long _lastDownloadEnd;
            private long _lastRequestedEnd;
            private double _bitrate;
            private bool _initialized;

            public RemoteFileBitrate(File file)
            {
                Update(file, 0);
            }

            /// <param name="requestedEnd">Where the window last asked for ends.</param>
            public double Update(File file, long requestedEnd)
            {
                ulong now = Logger.TickCount;

                var offset = file.Local.DownloadOffset;
                var end = offset + file.Local.DownloadedPrefixSize;

                // Measured only over time TDLib spent downloading. Once it has what was asked for
                // it waits, and the reader asks for more at the rate it plays, so time measured
                // across that would give the media's bitrate rather than the link's.
                var measurable = _initialized
                    && file.Local.IsDownloadingActive
                    && offset >= _lastDownloadOffset
                    && offset <= _lastDownloadEnd
                    && _lastDownloadEnd < _lastRequestedEnd;

                if (measurable)
                {
                    var delta = now - _lastUpdateTime;
                    if (delta < 100)
                    {
                        return _bitrate;
                    }

                    // The end of what is downloaded ahead of the reader. The offset moves with
                    // every read, and the prefix past it shrinks by as much, so the prefix alone
                    // counts what was already there as new.
                    var bytesDownloaded = end - _lastDownloadEnd;

                    if (bytesDownloaded > 0)
                    {
                        double instant = (bytesDownloaded * 8.0) / delta;

                        if (_bitrate == 0)
                        {
                            _bitrate = instant;
                        }
                        else
                        {
                            _bitrate = _alpha * instant + (1 - _alpha) * _bitrate;
                        }
                    }
                }

                _lastDownloadOffset = offset;
                _lastDownloadEnd = end;
                _lastRequestedEnd = requestedEnd;
                _lastUpdateTime = now;
                _initialized = true;

                return _bitrate;
            }
        }
    }
}
