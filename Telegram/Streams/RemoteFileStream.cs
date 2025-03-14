//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class RemoteFileStream : MediaInput
    {
        private readonly File _file;

        private readonly RemoteFileSource _source;

        private FileStreamFromApp _fileStream;
        private string _filePath;

        private bool _disposed;

        public RemoteFileStream(IClientService clientService, File file)
        {
            _file = file;
            _source = new RemoteFileSource(clientService, file);

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
                    _fileStream?.Close();

                    _fileStream = new FileStreamFromApp(path);
                    _filePath = path;
                }
                else if (_fileStream == null)
                {
                    return 0;
                }

                _fileStream.Seek(_source.Offset);
                _source.SeekCallback(_source.Offset + len);

                return _fileStream.Read((long)buf, len);
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
}
