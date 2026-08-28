//
// Copyright (c) Fela Ameghino 2015-2026
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
    /// <summary>
    /// The outline an <see cref="AnimatedImageSource"/> draws its placeholder from: the SVG path
    /// that getStickerOutlineSvgPath returns, or the contours of an <see cref="Outline"/>, which is
    /// all stickerSet.thumbnail_outline offers.
    /// </summary>
    /// <remarks>
    /// A union rather than two properties because there are three states to tell apart - not asked
    /// for yet, known to have none, and known - and two references cannot express them.
    /// </remarks>
    public readonly struct AnimatedImageOutline
    {
        // An SVG path or a Vector<ClosedVectorPath>. Both are reference types, so this holds a
        // reference to either and boxes neither.
        private readonly object _data;

        private AnimatedImageOutline(object data)
        {
            _data = data;
        }

        /// <summary>
        /// Known to have no outline, which getStickerOutlineSvgPath reports as an empty string.
        /// Distinct from the default, the state that asks for one.
        /// </summary>
        public static AnimatedImageOutline None => new(string.Empty);

        public bool IsReady => _data is not null;

        public static implicit operator AnimatedImageOutline(string svgPath) => new(svgPath);

        public static implicit operator AnimatedImageOutline(Vector<ClosedVectorPath> contours) => new(contours);

        public bool TryGetSvgPath(out string svgPath)
        {
            svgPath = _data as string;
            return svgPath?.Length > 0;
        }

        public bool TryGetContours(out Vector<ClosedVectorPath> contours)
        {
            contours = _data as Vector<ClosedVectorPath>;
            return contours?.Count > 0;
        }
    }

    [CreateFromString(MethodName = "Telegram.Streams.AnimatedImageSourceFactory.Create")]
    public abstract class AnimatedImageSource : IVideoAnimationSource
    {
        #region Properties

        public bool NeedsRepainting { get; set; }

        public AnimatedImageOutline Outline { get; set; }

        // Needed for Outline
        public int Width { get; set; }

        // Needed for Outline
        public int Height { get; set; }

        public event EventHandler OutlineChanged;

        protected void OnOutlineChanged()
        {
            OutlineChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual void RequestOutline()
        {

        }

        #endregion

        #region Lottie specific

        public IReadOnlyDictionary<string, int> Markers { get; set; }

        public IReadOnlyDictionary<int, int> ColorReplacements { get; set; }

        public FitzModifier FitzModifier { get; set; }

        #endregion

        public StickerFormat Format { get; protected set; }

        public abstract void SeekCallback(long offset);
        public abstract void ReadCallback(long count, long buffer, out long bytesRead);

        public abstract string FilePath { get; }
        public abstract long FileSize { get; }

        public abstract long Id { get; }

        public abstract long Offset { get; }

        public bool IsUnique { get; set; }

        public bool IsAnimated { get; set; } = true;

        public double SeekToSeconds { get; set; } = 0;

        public override bool Equals(object obj)
        {
            if (obj is AnimatedImageSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id && y.IsAnimated == IsAnimated;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return HashCode.Combine(Id, IsAnimated);
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
