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
        private readonly ManualResetEvent _event;
        private readonly object _stateLock = new();

        private readonly IClientService _clientService;
        private readonly File _file;
        private readonly int _priority;
        private readonly bool _adaptive;

        private readonly RemoteFileBitrate _bitrate;

        // Null unless this source owns the download, in which case ReadCallback uses the
        // window it is handed rather than one worked out here.
        private readonly RemoteFilePrefetch _prefetch;

        private readonly bool _streaming;

        private long _offset;
        private long _count;

        private bool _closed;
        private bool _tracked;

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
        public RemoteFileSource(IClientService clientService, File file, double duration, bool streaming = true)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;
            _file = file;
            _priority = 32;
            _adaptive = true;

            _bitrate = new RemoteFileBitrate(file);

            _streaming = streaming;

            if (streaming)
            {
                _prefetch = new RemoteFilePrefetch(file.Size, duration);
            }

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);

            TrackStreaming(true);
        }

        public RemoteFileSource(IClientService clientService, File file/*, int priority = 32*/)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;
            _file = file;
            _priority = 32;
            _adaptive = false;

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);

            TrackStreaming(true);
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
        /// Zero is the caller's end of stream: AsyncMediaPlayer turns a zero byte count
        /// into a zero length ReadFile, and libvlc reads that as the end of the media.
        /// It may therefore only be reported once there genuinely is nothing left; every
        /// other "nothing available yet" has to keep waiting instead.
        ///
        /// This is also what terminates the read loops. A zero count would satisfy
        /// MustWait without waiting and read back zero bytes, which is neither progress
        /// nor the end.
        /// </summary>
        private bool CanRead(long count)
        {
            return count > 0 && !AtEndOfStream;
        }

        public override void ReadCallback(long count, long buffer, out long bytesRead)
        {
            var started = Logger.TickCount;
            var waited = 0UL;

            while (true)
            {
                if (MustWait(count, PrefetchWindow(buffer)))
                {
                    var blocked = Logger.TickCount;
                    _event.WaitOne();
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

                // Nothing yet, and not the end: MustWait requested the range again on
                // the way through, so go back and wait for the update that answers it.
            }
        }

        private long PrefetchWindow(long buffer)
        {
            return _prefetch?.Window ?? buffer;
        }

        public async Task<long> ReadCallbackAsync(long count, long buffer)
        {
            while (true)
            {
                if (MustWait(count, PrefetchWindow(buffer)))
                {
                    await _event.WaitOneAsync();
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

        /// <summary>
        /// Asks for the range the reader is about to need.
        ///
        /// Sent directly rather than through IClientService.DownloadFile, which records
        /// the file as one that was asked for in its own right. This is a reader pulling
        /// in the parts it is about to read, and it is what Close is allowed to cancel.
        /// </summary>
        private void Download(long offset, long limit)
        {
            _clientService.Send(new DownloadFile(_file.Id, _priority, offset, limit, false));
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

            _bitrate?.Update(file);

            lock (_stateLock)
            {
                // No need to process the update if no one is waiting
                if (_event.WaitOne(0))
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

        /// <summary>
        /// Marks the file as one that is only being read, rather than downloaded in its
        /// own right. The download buttons hide a download that exists for a reader alone,
        /// which would otherwise flicker as the window this source asks for moves along.
        /// </summary>
        private void TrackStreaming(bool streaming)
        {
            if (_tracked == streaming)
            {
                return;
            }

            _tracked = streaming;
            _clientService.TrackStreamingFile(_file.Id, streaming);
        }

        public void Open()
        {
            lock (_stateLock)
            {
                _closed = false;

                // The player opens the media again to replay it, having closed it at the end.
                TrackStreaming(true);
            }

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

                TrackStreaming(false);

                //Logger.Debug($"Disposing the stream");
                UpdateManager.Unsubscribe(this, ref _fileToken);

                // Only the reader that owns the download stops it. The last window this
                // source asked for is still running, and nothing else ends it, so closing
                // the player would otherwise leave it fetching ahead of a video nobody is
                // watching. Sources serving one ranged request each share the file with
                // whoever else is reading it and must leave it alone.
                //
                // onlyIfStreaming keeps this to the download this source started: the same
                // file may also be downloading because the user asked for it, or because
                // auto-download picked it up, and neither ends when a player closes.
                if (_streaming)
                {
                    _clientService.CancelDownloadFile(_file, false, true);
                }

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
            private long _lastDownloadedPrefixSize;
            private double _bitrate;
            private bool _downloadingActive;
            private bool _initialized;

            public RemoteFileBitrate(File file)
            {
                Update(file);
            }

            public double Update(File file)
            {
                ulong now = Logger.TickCount;

                if (!_initialized)
                {
                    _lastDownloadOffset = file.Local.DownloadOffset;
                    _lastDownloadedPrefixSize = file.Local.DownloadedPrefixSize;
                    _lastUpdateTime = now;
                    _downloadingActive = file.Local.IsDownloadingActive;
                    _bitrate = 0;

                    _initialized = true;
                    return 0;
                }

                if (file.Local.IsDownloadingActive && !_downloadingActive)
                {
                    _lastDownloadOffset = file.Local.DownloadOffset;
                    _lastDownloadedPrefixSize = file.Local.DownloadedPrefixSize;
                    _lastUpdateTime = now;
                    _downloadingActive = true;
                    return _bitrate;
                }

                if (!file.Local.IsDownloadingActive)
                {
                    _downloadingActive = false;
                    return _bitrate;
                }

                var delta = now - _lastUpdateTime;
                if (delta < 100)
                {
                    return _bitrate;
                }

                long bytesDownloaded = 0;

                if (file.Local.DownloadOffset != _lastDownloadOffset)
                {
                    bytesDownloaded = file.Local.DownloadedPrefixSize;
                }
                else
                {
                    var currentPosition = file.Local.DownloadedPrefixSize;
                    var previousPosition = _lastDownloadedPrefixSize;
                    bytesDownloaded = currentPosition - previousPosition;
                }

                _lastDownloadOffset = file.Local.DownloadOffset;
                _lastDownloadedPrefixSize = file.Local.DownloadedPrefixSize;
                _lastUpdateTime = now;

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

                return _bitrate;
            }
        }
    }
}
