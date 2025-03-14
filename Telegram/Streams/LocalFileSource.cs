//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Path = System.IO.Path;

namespace Telegram.Streams
{
    public partial class LocalFileSource : AnimatedImageSource
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

        public override void ReadCallback(long count)
        {
            // Nothing
        }

        public override bool Equals(object obj)
        {
            if (obj is LocalFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.FilePath == FilePath;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return FilePath.GetHashCode();
        }
    }
}
