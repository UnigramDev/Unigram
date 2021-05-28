using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Native;
using Unigram.Services;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Unigram.Common
{
    public class RemoteFileStream : IRandomAccessStream
    {
        private readonly IProtoService _protoService;
        private readonly File _file;

        private readonly RemoteVideoSource _source;

        private IRandomAccessStream _fileStream;

        public RemoteFileStream(IProtoService protoService, File file, int duration)
        {
            _protoService = protoService;
            _file = file;

            _source = new RemoteVideoSource(protoService, file, duration);
        }

        public int FileId => _source.Id;

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

                    if (_fileStream == null)
                    {
                        var file = await _protoService.GetFileAsync(_file, false);
                        _fileStream = await file.OpenAsync(FileAccessMode.Read, StorageOpenOptions.AllowReadersAndWriters);
                    }

                    _fileStream.Seek((ulong)_source.Offset);
                    return await _fileStream.ReadAsync(buffer, count, options);
                }));
        }

        public void UpdateFile(File file)
        {
            _source.UpdateFile(file);
        }

        public void Dispose()
        {
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        #region Not Implemented

        public IRandomAccessStream CloneStream()
        {
            return this;
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

        private readonly IProtoService _protoService;

        private readonly File _file;

        private readonly int _chunk;
        private readonly object _readLock = new();

        private int _offset;
        private int _next;

        private int _bufferSize = 256 * 1024;

        public RemoteVideoSource(IProtoService protoService, File file, int duration)
        {
            _event = new ManualResetEvent(false);

            _protoService = protoService;

            _file = file;
            _chunk = (int)(file.Size / (duration / 10d));
        }

        public void SeekCallback(int offset)
        {
            _offset = offset;
        }

        public void ReadCallback(int count)
        {
            lock (_readLock)
            {
                _bufferSize = Math.Max(count, _bufferSize);

                var begin = _file.Local.DownloadOffset;
                var end = _file.Local.DownloadOffset + _file.Local.DownloadedPrefixSize;

                var inBegin = _offset >= begin;
                var inEnd = end >= _offset + count || end == _file.Size;
                var difference = end - _offset;

                if (_file.Local.Path.Length > 0 && (inBegin && inEnd) || _file.Local.IsDownloadingCompleted)
                {
                    if (difference < _chunk / 3 * 2 && _offset > _next)
                    {
                        _protoService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                        _next = _offset + _chunk / 3;
                    }
                }
                else
                {
                    _protoService.Send(new DownloadFile(_file.Id, 32, _offset, /*_chunk*/ 0, false));
                    _next = _offset + _chunk / 3;

                    _event.Reset();
                    _event.WaitOne();
                }
            }
        }

        public string FilePath => _file.Local.Path;
        public int FileSize => _file.Size;

        public int Offset => _offset;

        public int Id => _file.Id;

        public void UpdateFile(File file)
        {
            if (file.Id != _file.Id)
            {
                return;
            }

            _file.Update(file);

            var enough = file.Local.DownloadedPrefixSize >= _bufferSize;
            var end = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize == file.Size;

            if (file.Local.Path.Length > 0 && file.Local.DownloadOffset == _offset && (enough || end || file.Local.IsDownloadingCompleted))
            {
                _event.Set();
            }
        }
    }

    public class LocalVideoSource : IVideoAnimationSource
    {
        private readonly File _file;

        private int _offset;

        public LocalVideoSource(File file)
        {
            _file = file;
        }

        public string FilePath => _file.Local.Path;
        public int FileSize => _file.Size;

        public int Offset => _offset;

        public int Id => _file.Id;

        public void SeekCallback(int offset)
        {
            _offset = offset;
        }

        public void ReadCallback(int count)
        {
            // Nothing
        }
    }
}
