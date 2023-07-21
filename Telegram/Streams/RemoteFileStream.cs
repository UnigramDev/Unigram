//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Streams
{
    public class RemoteInputStream : MediaInput
    {
        private readonly IClientService _clientService;
        private readonly File _file;

        private readonly RemoteFileSource2 _source;

        private System.IO.Stream _fileStream;
        private string _filePath;

        private bool _disposed;

        public RemoteInputStream(IClientService clientService, File file)
        {
            _clientService = clientService;
            _file = file;

            _source = new RemoteFileSource2(clientService, file);
            CanSeek = true;
        }

        /// <summary>
        /// LibVLC calls this method when it wants to open the media
        /// </summary>
        /// <param name="size">This value must be filled with the length of the media (or ulong.MaxValue if unknown)</param>
        /// <returns><c>true</c> if the stream opened successfully</returns>
        public override bool Open(out ulong size)
        {
            size = (ulong)_file.Size;

            _source.Open();
            return true;
        }

        /// <summary>
        /// LibVLC calls this method when it wants to read the media
        /// </summary>
        /// <param name="buf">The buffer where read data must be written</param>
        /// <param name="len">The buffer length</param>
        /// <returns>strictly positive number of bytes read, 0 on end-of-stream, or -1 on non-recoverable error</returns>
        public unsafe override int Read(IntPtr buf, uint len)
        {
            try
            {
                _source.ReadCallback((int)len);

                if (_disposed || _source.Offset == _file.Size)
                {
                    return 0;
                }

                var path = _file.Local.Path;
                if (path.Length > 0 && !_source.IsCanceled && (_fileStream == null || _filePath != path))
                {
                    _fileStream?.Dispose();

                    _fileStream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
                    _filePath = path;
                }
                else if (_fileStream == null)
                {
                    return 0;
                }

                _fileStream.Seek(_source.Offset, System.IO.SeekOrigin.Begin);
                _source.SeekCallback(_source.Offset + len);

                return _fileStream.Read(new Span<byte>(buf.ToPointer(), (int)len));
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// LibVLC calls this method when it wants to seek to a specific position in the media
        /// </summary>
        /// <param name="offset">The offset, in bytes, since the beginning of the stream</param>
        /// <returns><c>true</c> if the seek succeeded, false otherwise</returns>
        public override bool Seek(ulong offset)
        {
            try
            {
                _source.SeekCallback((int)offset);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// LibVLC calls this method when it wants to close the media.
        /// </summary>
        public override void Close()
        {
            try
            {
                _fileStream?.Close();
                _fileStream = null;

                _source.Close();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

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
                    _source.SeekCallback(_source.Offset + count);
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
