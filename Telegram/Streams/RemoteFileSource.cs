//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public class RemoteFileSource : AnimatedImageSource
    {
        private readonly ManualResetEvent _event;

        private readonly IClientService _clientService;

        private readonly File _file;

        private readonly int _chunk;
        private readonly SemaphoreSlim _readLock = new(1, 1);

        private bool _canceled;

        private long _offset;
        private long _next;

        private long _bufferSize = 256 * 1024;

        private bool _disposed;

        private long _fileToken;

        public RemoteFileSource(IClientService clientService, File file, int duration)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;

            _file = file;
            _chunk = (int)(file.Size / (duration / 10d));

            if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
            }
        }

        public bool Wait()
        {
            if (_disposed)
            {
                return false;
            }

            return _readLock.Wait(0);
        }

        public override void SeekCallback(long offset)
        {
            _offset = offset;
        }

        public override void ReadCallback(long count)
        {
            _readLock.Wait();
            _bufferSize = Math.Max(count, _bufferSize);

            var begin = _file.Local.DownloadOffset;
            var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

            var inBegin = _offset >= begin;
            var inEnd = end >= _offset + count || end == _file.Size;
            var difference = end - _offset;

            if (_canceled)
            {
                _readLock.Release();
                return;
            }

            if (_file.Local.Path.Length > 0 && (inBegin && inEnd || _file.Local.IsDownloadingCompleted))
            {
                if (difference < _chunk / 3 * 2 && _offset > _next)
                {
                    _clientService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                    _next = _offset + _chunk / 3;
                }

                //Logger.Debug($"Enough data available, offset: {_offset}, next: {_next}, size: {_file.Size}");

                _readLock.Release();
            }
            else
            {
                _readLock.Release();
                _event.Reset();

                _clientService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                _next = _offset + _chunk / 3;

                //Logger.Debug($"Not enough data available, offset: {_offset}, next: {_next}, size: {_file.Size}");

                _event.WaitOne();
            }
        }

        public override string FilePath => _file.Local.Path;
        public override long FileSize => _file.Size;

        public override int Id => _file.Id;

        public override long Offset => _offset;

        public bool IsCanceled => _canceled;

        private void UpdateFile(object target, File file)
        {
            if (file.Id != _file.Id)
            {
                return;
            }

            var enough = file.Local.DownloadedPrefixSize >= _bufferSize;
            var end = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize == file.Size;

            if (file.Local.Path.Length > 0 && (file.Local.DownloadOffset == _offset && (enough || end) || file.Local.IsDownloadingCompleted))
            {
                //Logger.Debug($"Next chunk is available, offset: {_offset}, prefix: {file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                _event.Set();
            }
            //else
            //{
            //    Logger.Debug($"Next chunk is not available, offset: {_offset}, real: {file.Local.DownloadOffset}, prefix: {file.Local.DownloadedPrefixSize}, size: {_file.Size}, completed: {file.Local.IsDownloadingCompleted}");
            //}
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            //Logger.Debug($"Disposing the stream");
            UpdateManager.Unsubscribe(this, ref _fileToken);

            _canceled = true;
            _clientService.Send(new CancelDownloadFile(_file.Id, false));

            //_event.Dispose();
            //_readLock.Dispose();
        }
    }
}
