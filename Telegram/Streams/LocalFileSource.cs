//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Native.Media;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Path = System.IO.Path;

namespace Telegram.Streams
{
    public partial class LocalFileSource : AnimatedImageSource, IAsyncMediaPlayerSource
    {
        private long _offset;

        public LocalFileSource(File file)
        {
            if (file == null)
            {
                return;
            }

            FilePath = file.Local.Path;
            FileSize = file.Size;

            Format = PathToFormat(file.Local.Path);

            Id = file.Id;
        }

        public LocalFileSource(string path)
        {
            FilePath = UriToPath(path);
            Format = PathToFormat(path);
        }

        /// <param name="size">
        /// Required to play the file through <see cref="AsyncMediaPlayer"/>: it is what the
        /// open callback reports as the length of the media, and zero reads as an empty one.
        /// </param>
        public LocalFileSource(string path, long size)
            : this(path)
        {
            FileSize = size;
        }

        private static string UriToPath(string uri)
        {
            var split = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);

            switch (split[0])
            {
                case "ms-appx:":
                    split[0] = Package.Current.InstalledLocation.Path;
                    return Path.Combine(split);
                default:
                    return uri;
            }
        }

        private static StickerFormat PathToFormat(string path)
        {
            if (path.HasExtension(".tgs", ".json"))
            {
                return new StickerFormatTgs();
            }
            else if (path.HasExtension(".webp"))
            {
                return new StickerFormatWebp();
            }

            return new StickerFormatWebm();
        }

        public override string FilePath { get; }
        public override long FileSize { get; }

        public override long Id { get; }

        public override long Offset => _offset;

        public override void SeekCallback(long offset)
        {
            _offset = offset;
        }

        public override void ReadCallback(long count, long buffer, out long bytesRead)
        {
            bytesRead = count;
        }

        // Replaying opens the media again, and the offset left behind is the end of the file:
        // the read callback would seek there, read nothing, and libvlc would take that for the
        // end of the stream before it could fill its buffer.
        public void Open()
        {
            SeekCallback(0);
        }

        // Nothing to release: the read callback owns the file handle it opens from FilePath.
        public void Close() { }

        public override bool Equals(object obj)
        {
            if (obj is LocalFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.FilePath == FilePath && y.IsAnimated == IsAnimated;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return HashCode.Combine(FilePath, IsAnimated);
        }
    }
}
