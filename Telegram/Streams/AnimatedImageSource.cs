//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Native;
using Windows.Foundation.Metadata;

namespace Telegram.Streams
{
    [CreateFromString(MethodName = "Telegram.Streams.AnimatedImageSourceFactory.Create")]
    public abstract class AnimatedImageSource : IVideoAnimationSource
    {
        public abstract void SeekCallback(long offset);
        public abstract void ReadCallback(long count);

        public abstract string FilePath { get; }
        public abstract long FileSize { get; }

        public abstract int Id { get; }

        public abstract long Offset { get; }
    }

    public static class AnimatedImageSourceFactory
    {
        public static AnimatedImageSource Create(string value)
        {
            return new LocalFileSource(value);
        }
    }
}
