//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Entities;
using Windows.UI;

namespace Telegram.Controls
{
    /// <summary>
    /// A story's venue as it is drawn, in the editor and into the story alike: a pin and the
    /// venue's name. It is laid out in its own units, a 240th of the image's width at a scale of
    /// one, which is how the other clients size it.
    /// </summary>
    public sealed partial class PaintLocationVisual : IDisposable
    {
        public const float Unit = PaintEntityRenderer.Reference / 240;

        // The widest it may grow.
        private const float MaxWidth = 200;

        private const float PaddingLeft = 4;
        private const float PaddingTop = 4.33f;
        private const float PaddingRight = 7.66f;
        private const float PaddingBottom = 3;
        private const float IconSize = 21.33f;
        private const float IconPadding = 3.25f;

        private const float LineHeight = 28.1f;

        // Shrunk further than this, a long name goes on two lines instead.
        private const float MinimumTextScale = 0.4f;

        private static readonly Color PinTop = Color.FromArgb(0xFF, 0x0B, 0xC4, 0xBA);
        private static readonly Color PinBottom = Color.FromArgb(0xFF, 0x11, 0x96, 0xE4);

        // The pin, on a 72 unit square.
        private const string PinData = "M36,9.33333333 C24.8,9.33333333 14.6666667,17.92 14.6666667,31.2 C14.6666667,39.68 21.2,51.7866667 34.24,63.28 C35.2533333,64.16 36.7733333,64.16 37.7866667,63.28 C50.8,51.7866667 57.3333333,39.68 57.3333333,31.2 C57.3333333,17.92 47.2,9.33333333 36,9.33333333 Z M36,37 C32.15,37 29,33.85 29,30 C29,26.15 32.15,23 36,23 C39.85,23 43,26.15 43,30 C43,33.85 39.85,37 36,37 Z";

        private readonly CanvasTextLayout[] _lines;
        private readonly CanvasGeometry _pin;
        private readonly float _textScale;
        private readonly float _textTop;
        private readonly float _lineHeight;

        private PaintLocationVisual(ICanvasResourceCreator creator, PaintEntity entity)
        {
            using (var geometry = CompositionPathParser.Parse(creator, PinData))
            {
                _pin = geometry.Transform(Matrix3x2.CreateScale(IconSize / 72));
            }

            var text = (entity.LocationTitle ?? string.Empty).ToUpper();
            var chrome = PaddingLeft + IconSize + IconPadding + PaddingRight;
            var room = MaxWidth - chrome;

            var line = PaintLinkVisual.CreateLabelLine(creator, text, float.PositiveInfinity);
            var width = (float)line.LayoutBounds.Width;

            _lineHeight = (float)line.LayoutBounds.Height;

            if (width * MinimumTextScale <= room)
            {
                _lines = new[] { line };
            }
            else
            {
                line.Dispose();

                var split = Split(text);

                _lines = new[]
                {
                    PaintLinkVisual.CreateLabelLine(creator, split.Item1, float.PositiveInfinity),
                    PaintLinkVisual.CreateLabelLine(creator, split.Item2, float.PositiveInfinity)
                };

                width = (float)Math.Max(_lines[0].LayoutBounds.Width, _lines[1].LayoutBounds.Width);
            }

            _textScale = width > 0 ? Math.Min(1, room / width) : 1;

            var textHeight = _lines.Length > 1
                ? _lineHeight * 2 * _textScale
                : LineHeight * _textScale;

            var height = PaddingTop + PaddingBottom + Math.Max(IconSize, textHeight);

            Size = new Vector2(chrome + width * _textScale, height);

            // One line is centred by its capitals; two by their lines, as a block.
            _textTop = _lines.Length > 1
                ? (height - _lineHeight * 2 * _textScale) / 2 / _textScale
                : PaintLinkVisual.GetCapitalsTop(creator, height / 2 / _textScale);
        }

        /// <summary>
        /// The size it is drawn at, in its own units.
        /// </summary>
        public Vector2 Size { get; }

        public float Radius => Size.Y * 0.2f;

        public static PaintLocationVisual Create(ICanvasResourceCreator creator, PaintEntity entity)
        {
            return new PaintLocationVisual(creator, entity);
        }

        // At the space nearest the middle, or the middle itself when there is none.
        private static (string, string) Split(string text)
        {
            var middle = text.Length / 2;
            var best = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ' && (best < 0 || Math.Abs(i - middle) < Math.Abs(best - middle)))
                {
                    best = i;
                }
            }

            return best > 0
                ? (text.Substring(0, best), text.Substring(best + 1))
                : (text.Substring(0, middle), text.Substring(middle));
        }

        /// <summary>
        /// Draws it in its own units, through whatever transform <paramref name="session"/> has.
        /// </summary>
        public void Draw(CanvasDrawingSession session, PaintEntity entity)
        {
            Color background, foreground;
            bool tintPin;

            switch (entity.WidgetStyle)
            {
                case PaintWidgetStyle.Translucent:
                    background = Color.FromArgb(0x4C, 0, 0, 0);
                    foreground = Colors.White;
                    tintPin = false;
                    break;
                case PaintWidgetStyle.White:
                    background = Colors.White;
                    foreground = Colors.Black;
                    tintPin = false;
                    break;
                default:
                    background = Colors.Black;
                    foreground = Colors.White;
                    tintPin = true;
                    break;
            }

            var radius = Radius;
            session.FillRoundedRectangle(0, 0, Size.X, Size.Y, radius, radius, background);

            var previous = session.Transform;
            session.Transform = Matrix3x2.CreateTranslation(PaddingLeft, (Size.Y - IconSize) / 2) * previous;

            if (tintPin)
            {
                session.FillGeometry(_pin, foreground);
            }
            else
            {
                using var gradient = new CanvasLinearGradientBrush(session, PinTop, PinBottom)
                {
                    StartPoint = Vector2.Zero,
                    EndPoint = new Vector2(0, IconSize * 64 / 72)
                };

                session.FillGeometry(_pin, gradient);
            }

            session.Transform = Matrix3x2.CreateScale(_textScale) * Matrix3x2.CreateTranslation(PaddingLeft + IconSize + IconPadding, 0) * previous;

            for (int i = 0; i < _lines.Length; i++)
            {
                session.DrawTextLayout(_lines[i], 0, _textTop + i * _lineHeight, foreground);
            }

            session.Transform = previous;
        }

        public void Dispose()
        {
            foreach (var line in _lines)
            {
                line.Dispose();
            }

            _pin.Dispose();
        }
    }
}
