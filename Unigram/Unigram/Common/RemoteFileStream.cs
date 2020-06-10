using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Unigram.Common
{
    public class RemoteFileStream : IRandomAccessStream
    {
        private readonly ManualResetEvent _event;

        private readonly IProtoService _protoService;

        private File _file;
        private readonly TimeSpan _duration;

        private IRandomAccessStream _fileStream;

        private int _offset;
        private int _chunk;
        private int _next;

        private int _bufferSize = 256 * 1024;

        public RemoteFileStream(IProtoService protoService, File file, TimeSpan duration)
        {
            _event = new ManualResetEvent(false);

            _protoService = protoService;

            _file = file;
            _duration = duration;

            _chunk = (int)(file.Size / _duration.TotalMinutes);
        }

        public int FileId => _file.Id;

        public bool CanRead => true;

        public bool CanWrite => false;

        public ulong Position => (ulong)_offset;

        public ulong Size
        {
            get => (ulong)_file.Size;
            set => throw new NotImplementedException();
        }

        public void Seek(ulong position)
        {
            _offset = (int)position;
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            _bufferSize = Math.Max((int)count, _bufferSize);

            return AsyncInfo.Run<IBuffer, uint>((token, progress) =>
                Task.Run(async () =>
                {
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

                    if (_fileStream == null)
                    {
                        var file = await StorageFile.GetFileFromPathAsync(_file.Local.Path);
                        _fileStream = await file.OpenAsync(FileAccessMode.Read, StorageOpenOptions.AllowReadersAndWriters);
                    }

                    _fileStream.Seek((ulong)_offset);
                    return await _fileStream.ReadAsync(buffer, count, options);
                }));
        }

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
}
