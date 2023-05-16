//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Streams
{
    public class RemoteFileStream : IRandomAccessStream
    {
        private readonly IClientService _clientService;
        private readonly File _file;

        private readonly RemoteFileSource _source;

        private IRandomAccessStream _fileStream;
        private string _filePath;

        private bool _disposed;

        public RemoteFileStream(IClientService clientService, File file, int duration)
        {
            _clientService = clientService;
            _file = file;

            _source = new RemoteFileSource(clientService, file, duration);
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
                        _fileStream?.Dispose();

                        var cached = await _clientService.GetFileAsync(_file, false);

                        _fileStream = await cached.OpenAsync(FileAccessMode.Read, StorageOpenOptions.AllowReadersAndWriters);
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
            if (_disposed)
            {
                return;
            }

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
}
