//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Common
{
    public class RemoteFileStream : IRandomAccessStream
    {
        private readonly IClientService _clientService;
        private readonly File _file;

        private readonly RemoteVideoSource _source;

        private IRandomAccessStream _fileStream;
        private string _filePath;

        private bool _disposed;

        public RemoteFileStream(IClientService clientService, File file, int duration)
        {
            _clientService = clientService;
            _file = file;

            _source = new RemoteVideoSource(clientService, file, duration);
        }

        public File File => _file;

        public int FileId => _file.Id;

        public bool CanRead => true;

        public bool CanWrite => false;

        public ulong Position => (ulong)_source.Offset;

        public ulong Size
        {
            get => (ulong)_source.FileSize;
            set => throw new NotImplementedException();
        }

        public void Seek(ulong position)
        {
            _source.SeekCallback((int)position);
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return AsyncInfo.Run<IBuffer, uint>((token, progress) =>
                Task.Run(async () =>
                {
                    _source.ReadCallback((int)count);

                    if (_disposed)
                    {
                        return BufferSurface.Create(0);
                    }

                    var path = _file.Local.Path;
                    if (path.Length > 0 && !_source.IsCanceled && (_fileStream == null || _filePath != path))
                    {
                        if (_fileStream != null)
                        {
                            _fileStream.Dispose();
                        }

                        var file = await _clientService.GetFileAsync(_file, false);

                        _fileStream = await file.OpenAsync(FileAccessMode.Read, StorageOpenOptions.AllowReadersAndWriters);
                        _filePath = path;
                    }
                    else if (_fileStream == null)
                    {
                        return BufferSurface.Create(0);
                    }

                    _fileStream.Seek((ulong)_source.Offset);
                    return await _fileStream.ReadAsync(buffer, count, options);
                }));
        }

        public void Dispose()
        {
            _disposed = true;

            if (_source.Wait())
            {
                _source.Dispose();

                if (_fileStream != null)
                {
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }
        }

        #region Not Implemented

        [DebuggerStepThrough]
        public IRandomAccessStream CloneStream()
        {
            throw new NotImplementedException();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class RemoteVideoSource : IVideoAnimationSource
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

        public RemoteVideoSource(IClientService clientService, File file, int duration)
        {
            _event = new ManualResetEvent(false);

            _clientService = clientService;

            _file = file;
            _chunk = (int)(file.Size / (duration / 10d));

            if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Subscribe(this, clientService, file, UpdateFile);
            }
        }

        public bool Wait() => _readLock.Wait(0);

        public void SeekCallback(long offset)
        {
            _offset = offset;
        }

        public void ReadCallback(long count)
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

            if (_file.Local.Path.Length > 0 && ((inBegin && inEnd) || _file.Local.IsDownloadingCompleted))
            {
                if (difference < _chunk / 3 * 2 && _offset > _next)
                {
                    _clientService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                    _next = _offset + _chunk / 3;
                }

                //Logs.Logger.Debug($"Enough data available, offset: {_offset}, next: {_next}, size: {_file.Size}");

                _readLock.Release();
            }
            else
            {
                _readLock.Release();
                _event.Reset();

                _clientService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                _next = _offset + _chunk / 3;

                //Logs.Logger.Debug($"Not enough data available, offset: {_offset}, next: {_next}, size: {_file.Size}");

                _event.WaitOne();
            }
        }

        public string FilePath => _file.Local.Path;
        public long FileSize => _file.Size;

        public long Offset => _offset;

        public int Id => _file.Id;

        public bool IsCanceled => _canceled;

        private void UpdateFile(object target, File file)
        {
            if (file.Id != _file.Id)
            {
                return;
            }

            var enough = file.Local.DownloadedPrefixSize >= _bufferSize;
            var end = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize == file.Size;

            if (file.Local.Path.Length > 0 && ((file.Local.DownloadOffset == _offset && (enough || end)) || file.Local.IsDownloadingCompleted))
            {
                //Logs.Logger.Debug($"Next chunk is available, offset: {_offset}, prefix: {file.Local.DownloadedPrefixSize}, size: {_file.Size}");
                _event.Set();
            }
            //else
            //{
            //    Logs.Logger.Debug($"Next chunk is not available, offset: {_offset}, real: {file.Local.DownloadOffset}, prefix: {file.Local.DownloadedPrefixSize}, size: {_file.Size}, completed: {file.Local.IsDownloadingCompleted}");
            //}
        }

        public void Dispose()
        {
            //Logs.Logger.Debug($"Disposing the stream");

            _canceled = true;
            _clientService.Send(new CancelDownloadFile(_file.Id, false));

            _event.Dispose();
            _readLock.Dispose();
        }
    }

    public class LocalVideoSource : IVideoAnimationSource
    {
        private readonly File _file;

        private long _offset;

        public LocalVideoSource(File file)
        {
            _file = file;
        }

        public string FilePath => _file.Local.Path;
        public long FileSize => _file.Size;

        public long Offset => _offset;

        public int Id => _file.Id;

        public void SeekCallback(long offset)
        {
            _offset = offset;
        }

        public void ReadCallback(long count)
        {
            // Nothing
        }
    }
}
