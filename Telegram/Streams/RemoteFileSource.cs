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
        private readonly double _duration;
        private readonly int _priority;
        private readonly bool _adaptive;

        private readonly RemoteFileBitrate _bitrate;

        private long _offset;
        private long _count;

        private bool _closed;

        private long _fileToken;

        public RemoteFileSource(IClientService clientService, File file, double duration)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;
            _file = file;
            _duration = duration;
            _priority = 32;
            _adaptive = true;

            _bitrate = new RemoteFileBitrate(file);

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
        }

        public RemoteFileSource(IClientService clientService, File file/*, int priority = 32*/)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;
            _file = file;
            _duration = 0;
            _priority = 32;
            _adaptive = false;

            Format = new StickerFormatWebm();
            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
        }

        public override void SeekCallback(long offset)
        {
            lock (_stateLock)
            {
                _offset = offset;

                if (_file.Local.CanBeDownloaded && !_file.Local.IsDownloadingCompleted && !_adaptive)
                {
                    _clientService.DownloadFile(_file.Id, _priority, offset, 0, false);
                }
            }
        }

        public override void ReadCallback(long count, long buffer, out long bytesRead)
        {
            if (MustWait(count, buffer))
            {
                _event.WaitOne();
            }

            bytesRead = DownloadedBytes;
        }

        public async Task<long> ReadCallbackAsync(long count, long buffer)
        {
            if (MustWait(count, buffer))
            {
                await _event.WaitOneAsync();
            }

            return DownloadedBytes;
        }

        public double Duration => _duration;

        public double DownloadRate => _bitrate?.CurrentBitrate ?? 0;

        public long DownloadedBytes => CalculateDownloadedBytes();

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

            var begin = _file.Local.DownloadOffset;
            var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

            var inBegin = _offset >= begin;
            var inEnd = end >= _offset;

            if (_file.Local.Path.Length > 0 && inBegin && inEnd)
            {
                if (_file.Local.IsDownloadingCompleted)
                {
                    return Math.Max(0, _file.Size - _offset);
                }
                else
                {
                    return Math.Max(0, end - _offset);
                }
            }

            using var ev = new ManualResetEventSlim(false);
            var buffered = 0L;

            _clientService.Send(new GetFileDownloadedPrefixSize(_file.Id, _offset), result =>
            {
                if (result is FileDownloadedPrefixSize prefixSize)
                {
                    buffered = prefixSize.Size;
                }
                ev.Set();
            });

            ev.Wait(500);
            return buffered;
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
                    _clientService.DownloadFile(_file.Id, _priority, _offset, buffer, false);

                    //Logger.Debug($"Next chunk is available for {_file.Id}, offset: {_offset}, limit: {buffer}, count: {count}, download: {_file.Local.DownloadOffset}, prefix: {_file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                    return false;
                }

                // Reset event before requesting download to avoid race condition
                _event.Reset();
                _count = count;

                _clientService.DownloadFile(_file.Id, _priority, _offset, buffer, false);

                //Logger.Debug($"Not enough data available for {_file.Id}, offset: {_offset}, limit: {buffer}, count: {count}, download: {_file.Local.DownloadOffset}, prefix: {_file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                return true;
            }
        }

        public override string FilePath => _file.Local.Path;
        public override long FileSize => _file.Size;

        public override long Id => _file.Id;

        public override long Offset => _offset;

        private void UpdateFile(object target, File file)
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

        public void Open()
        {
            lock (_stateLock)
            {
                _closed = false;
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

                //Logger.Debug($"Disposing the stream");
                UpdateManager.Unsubscribe(this, ref _fileToken);

                //if (cancel)
                //{
                //    _canceled = true;
                //    _clientService.Send(new CancelDownloadFile(_file.Id, false));
                //}

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
