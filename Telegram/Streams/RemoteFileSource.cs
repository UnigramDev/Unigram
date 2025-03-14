//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class RemoteFileSource : AnimatedImageSource
    {
        private readonly ManualResetEvent _event;

        private readonly IClientService _clientService;

        private readonly File _file;

        private bool _canceled;

        private long _offset;
        private long _next;

        private bool _closed;

        private long _fileToken;

        private readonly int _priority;
        private readonly bool _limit;

        public RemoteFileSource(IClientService clientService, File file, int priority = 32, bool limit = false)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;
            _file = file;
            _priority = priority;
            _limit = limit;

            Format = new StickerFormatWebm();

            //if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
            }
        }

        public override void SeekCallback(long offset)
        {
            _offset = offset;

            if (_file.Local.CanBeDownloaded && !_file.Local.IsDownloadingCompleted && !_limit)
            {
                _clientService.Send(new DownloadFile(_file.Id, _priority, offset, 0, false));
            }
        }

        public override void ReadCallback(long count)
        {
            if (MustWait(count))
            {
                _event.WaitOne();
            }
        }

        public Task ReadCallbackAsync(long count)
        {
            if (MustWait(count))
            {
                return _event.WaitOneAsync();
            }

            return Task.CompletedTask;
        }

        protected bool MustWait(long count)
        {
            var begin = _file.Local.DownloadOffset;
            var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

            var inBegin = _offset >= begin;
            var inEnd = end >= _offset + count || end == _file.Size;
            var difference = end - _offset;

            if (_canceled)
            {
                return false;
            }

            if (_file.Local.Path.Length > 0 && (inBegin && inEnd || _file.Local.IsDownloadingCompleted))
            {
                return false;
            }

            _event.Reset();

            _clientService.Send(new DownloadFile(_file.Id, 32, _offset, _limit ? count : 0, false));
            _next = count;

            //Logger.Debug($"Not enough data available, offset: {_offset}, next: {_next}, size: {_file.Size}");

            return true;
        }

        public override string FilePath => _file.Local.Path;
        public override long FileSize => _file.Size;

        public override long Id => _file.Id;

        public override long Offset => _offset;

        public bool IsCanceled => _canceled;

        private void UpdateFile(object target, File file)
        {
            if (file.Id != _file.Id)
            {
                return;
            }

            var enough = file.Local.DownloadedPrefixSize >= _next;
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

        public void Open()
        {
            _closed = false;
            _canceled = false;

            SeekCallback(0);
        }

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            //Logger.Debug($"Disposing the stream");
            UpdateManager.Unsubscribe(this, ref _fileToken);

            _canceled = true;
            _clientService.Send(new CancelDownloadFile(_file.Id, false));

            _event.Set();

            //_event.Dispose();
            //_readLock.Dispose();
        }
    }
}
