//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using RLottie;
using System;
using System.Collections.Generic;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.Foundation.Metadata;

namespace Telegram.Streams
{
    [CreateFromString(MethodName = "Telegram.Streams.AnimatedImageSourceFactory.Create")]
    public abstract class AnimatedImageSource : IVideoAnimationSource
    {
        #region Properties

        public bool NeedsRepainting { get; set; }

        public IList<ClosedVectorPath> Outline { get; set; }

        // Needed for Outline
        public int Width { get; set; }

        // Needed for Outline
        public int Height { get; set; }

        public event EventHandler OutlineChanged;

        protected void OnOutlineChanged()
        {
            OutlineChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Lottie specific

        public IReadOnlyDictionary<string, int> Markers { get; set; }

        public IReadOnlyDictionary<int, int> ColorReplacements { get; set; }

        public FitzModifier FitzModifier { get; set; }

        #endregion

        public StickerFormat Format { get; protected set; }

        public abstract void SeekCallback(long offset);
        public abstract void ReadCallback(long count);

        public abstract string FilePath { get; }
        public abstract long FileSize { get; }

        public abstract long Id { get; }

        public abstract long Offset { get; }

        public bool IsUnique { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is AnimatedImageSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return Id.GetHashCode();
        }
    }

    public static class AnimatedImageSourceFactory
    {
        public static AnimatedImageSource Create(string value)
        {
            return new LocalFileSource(value);
        }
    }
}
