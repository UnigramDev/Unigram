//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Entities;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace Telegram.Controls
{
    /// <summary>
    /// A story's link as it is drawn, in the editor and into the story alike: a label with the
    /// link's icon, or a card with the page's preview. It is laid out in its own units, a 360th of
    /// the image's width at a scale of one, which is how the other clients size it.
    /// </summary>
    public sealed partial class PaintLinkVisual : IDisposable
    {
        public const float Unit = PaintEntityRenderer.Reference / 360;

        // The widest either may grow.
        private const float MaxWidth = 304;

        private const float LabelPaddingLeft = 4;
        private const float LabelPaddingTop = 4.33f;
        private const float LabelPaddingRight = 7.66f;
        private const float LabelPaddingBottom = 3;
        private const float IconSize = 30;
        private const float IconPadding = 3.25f;

        // The line the label's text is centred in, as tall as the other clients' font makes it.
        private const float LabelLineHeight = 28.1f;

        private const float CardRadius = 16.66f;
        private const float PhotoSide = 48;
        private const float PhotoRadius = 4;

        private static readonly Color LabelBlue = Color.FromArgb(0xFF, 0x33, 0x91, 0xD4);
        private static readonly Color CardLink = Color.FromArgb(0xFF, 0x1A, 0x9C, 0xFF);
        private static readonly Color CardDark = Color.FromArgb(0xFF, 0x20, 0x24, 0x29);
        private static readonly Color CardText = Color.FromArgb(0xFF, 0x33, 0x33, 0x33);

        // The link icon, on a 72 unit square.
        private const string IconData = "M23.8310119,28.1628915 C25.0611634,29.393043 25.0611634,31.3875127 23.8310119,32.6176642 L20.5497168,35.8989592 C16.1172661,40.3314099 16.1172661,47.5178325 20.5497168,51.9502832 C24.9821675,56.3827339 32.1685901,56.3827339 36.6010408,51.9502832 L39.8823358,48.6689881 C41.1124873,47.4388366 43.106957,47.4388366 44.3371085,48.6689881 C45.56726,49.8991397 45.56726,51.8936093 44.3371085,53.1237609 L41.0558135,56.4050559 C34.1630597,63.2978096 22.9876979,63.2978096 16.0949441,56.4050559 C9.20219036,49.5123021 9.20219036,38.3369403 16.0949441,31.4441865 L19.3762391,28.1628915 C20.6063907,26.93274 22.6008603,26.93274 23.8310119,28.1628915 Z M55.9050559,16.5949441 C62.7978096,23.4876979 62.7978096,34.6630597 55.9050559,41.5558135 L52.6237609,44.8371085 C51.3936093,46.06726 49.3991397,46.06726 48.1689881,44.8371085 C46.9388366,43.606957 46.9388366,41.6124873 48.1689881,40.3823358 L51.4502832,37.1010408 C55.8827339,32.6685901 55.8827339,25.4821675 51.4502832,21.0497168 C47.0178325,16.6172661 39.8314099,16.6172661 35.3989592,21.0497168 L32.1176642,24.3310119 C30.8875127,25.5611634 28.893043,25.5611634 27.6628915,24.3310119 C26.43274,23.1008603 26.43274,21.1063907 27.6628915,19.8762391 L30.9441865,16.5949441 C37.8369403,9.70219036 49.0123021,9.70219036 55.9050559,16.5949441 Z M44.1647649,28.3352351 C45.4730213,29.6434914 45.4730213,31.7645941 44.1647649,33.0728505 L32.5728505,44.6647649 C31.2645941,45.9730213 29.1434914,45.9730213 27.8352351,44.6647649 C26.5269787,43.3565086 26.5269787,41.2354059 27.8352351,39.9271495 L39.4271495,28.3352351 C40.7354059,27.0269787 42.8565086,27.0269787 44.1647649,28.3352351 Z";

        private readonly bool _card;

        // The label.
        private CanvasTextLayout _label;
        private CanvasGeometry _icon;
        private float _labelTop;

        // The card.
        private CanvasTextLayout _message;
        private CanvasTextLayout _siteName;
        private CanvasTextLayout _title;
        private CanvasTextLayout _description;
        private float _previewHeight;
        private float _photoHeight;
        private bool _hasPhoto;
        private bool _smallPhoto;

        private PaintLinkVisual(bool card)
        {
            _card = card;
        }

        /// <summary>
        /// The size it is drawn at, in its own units.
        /// </summary>
        public Vector2 Size { get; private set; }

        /// <summary>
        /// The radius of its corners, in its own units, which its story area takes too.
        /// </summary>
        public float Radius => _card ? CardRadius : Size.Y * 0.2f;

        /// <summary>
        /// The radius of the corners of a link of <paramref name="size"/>, in its own units.
        /// </summary>
        public static float GetRadius(PaintEntity entity, Vector2 size)
        {
            return entity.LinkHasPreview ? CardRadius : size.Y * 0.2f;
        }

        public static PaintLinkVisual Create(ICanvasResourceCreator creator, PaintEntity entity)
        {
            var visual = new PaintLinkVisual(entity.LinkHasPreview);

            if (visual._card)
            {
                visual.LayoutCard(creator, entity);
            }
            else
            {
                visual.LayoutLabel(creator, entity);
            }

            return visual;
        }

        private void LayoutLabel(ICanvasResourceCreator creator, PaintEntity entity)
        {
            var text = string.IsNullOrEmpty(entity.LinkName)
                ? WithoutScheme(entity.LinkUrl ?? string.Empty).ToUpperInvariant()
                : entity.LinkName;

            var chrome = LabelPaddingLeft + IconSize + IconPadding + LabelPaddingRight;

            _label = CreateLine(creator, text, "Bahnschrift", FontWeights.Bold, FontStretch.Condensed, 24, MaxWidth - chrome);

            using (var geometry = CompositionPathParser.Parse(creator, IconData))
            {
                _icon = geometry.Transform(Matrix3x2.CreateScale(IconSize / 72));
            }

            var width = chrome + (float)Math.Min(_label.LayoutBounds.Width, MaxWidth - chrome);
            var height = LabelPaddingTop + LabelPaddingBottom + Math.Max(IconSize, LabelLineHeight);

            // Centred by its capitals, from cap height to baseline, whatever the font's own line.
            using var capitals = CreateLine(creator, "H", "Bahnschrift", FontWeights.Bold, FontStretch.Condensed, 24, MaxWidth);

            var baseline = capitals.LineMetrics[0].Baseline;
            var capHeight = baseline - (float)capitals.DrawBounds.Top;

            _labelTop = height / 2 - baseline + capHeight / 2;

            Size = new Vector2(width, height);
        }

        private void LayoutCard(ICanvasResourceCreator creator, PaintEntity entity)
        {
            var text = string.IsNullOrEmpty(entity.LinkName)
                ? entity.LinkUrl ?? string.Empty
                : entity.LinkName;

            _hasPhoto = !string.IsNullOrEmpty(entity.LinkPhotoRemoteId);
            _smallPhoto = !entity.LinkLargePhoto;

            // Beside a small photo, the text keeps clear of it.
            var aside = _hasPhoto && _smallPhoto ? PhotoSide + 12 : 0;

            _message = CreateLine(creator, text, "Segoe UI", FontWeights.Normal, FontStretch.Normal, 16, MaxWidth - 20);

            var width = Math.Min((float)_message.LayoutBounds.Width + 20, MaxWidth);
            var height = 7.33f + (float)_message.LayoutBounds.Height + 7;

            var preview = 5.66f;

            if (!string.IsNullOrEmpty(entity.LinkSiteName))
            {
                _siteName = CreateLine(creator, entity.LinkSiteName, "Segoe UI", FontWeights.SemiBold, FontStretch.Normal, 14, MaxWidth - 40 - aside);
                width = Math.Max(width, Math.Min((float)_siteName.LayoutBounds.Width + 40 + aside, MaxWidth));
                preview += (float)_siteName.LayoutBounds.Height + 2.66f;
            }

            if (!string.IsNullOrEmpty(entity.LinkTitle))
            {
                _title = CreateLine(creator, entity.LinkTitle, "Segoe UI", FontWeights.SemiBold, FontStretch.Normal, 14, MaxWidth - 40 - aside);
                width = Math.Max(width, Math.Min((float)_title.LayoutBounds.Width + 40 + aside, MaxWidth));
                preview += (float)_title.LayoutBounds.Height + 2.66f;
            }

            if (!string.IsNullOrEmpty(entity.LinkDescription))
            {
                _description = CreateParagraph(creator, entity.LinkDescription, 14, MaxWidth - 40 - aside, 4);
                width = Math.Max(width, Math.Min((float)_description.LayoutBounds.Width + 40 + aside, MaxWidth));
                preview += (float)_description.LayoutBounds.Height + 2.66f;
            }

            if (_hasPhoto && !_smallPhoto)
            {
                var photoWidth = Math.Max(0, width - 40);

                _photoHeight = entity.LinkPhotoWidth > 0 && entity.LinkPhotoHeight > 0
                    ? Math.Min(photoWidth / entity.LinkPhotoWidth * entity.LinkPhotoHeight, 200)
                    : 120;

                preview += _photoHeight + 2.66f;
            }

            preview += 7;

            // A small photo sits in the block's corner, which has to be tall enough for it.
            if (_hasPhoto && _smallPhoto)
            {
                preview = Math.Max(preview, PhotoSide + 12);
            }

            _previewHeight = preview;

            Size = new Vector2(width, height + preview + 11);
        }

        /// <summary>
        /// Draws it in its own units, through whatever transform <paramref name="session"/> has.
        /// The preview's photo is left out while <paramref name="photo"/> is null.
        /// </summary>
        public void Draw(CanvasDrawingSession session, PaintEntity entity, CanvasBitmap photo)
        {
            if (_card)
            {
                DrawCard(session, entity, photo);
            }
            else
            {
                DrawLabel(session, entity);
            }
        }

        private void DrawLabel(CanvasDrawingSession session, PaintEntity entity)
        {
            Color background, foreground;

            switch (entity.WidgetStyle)
            {
                case PaintWidgetStyle.Translucent:
                    background = Color.FromArgb(0x4C, 0, 0, 0);
                    foreground = Colors.White;
                    break;
                case PaintWidgetStyle.White:
                    background = Colors.White;
                    foreground = LabelBlue;
                    break;
                default:
                    background = Colors.Black;
                    foreground = Colors.White;
                    break;
            }

            var radius = Radius;

            session.FillRoundedRectangle(0, 0, Size.X, Size.Y, radius, radius, background);

            var previous = session.Transform;
            session.Transform = Matrix3x2.CreateTranslation(LabelPaddingLeft, (Size.Y - IconSize) / 2) * previous;
            session.FillGeometry(_icon, foreground);
            session.Transform = previous;

            session.DrawTextLayout(_label, LabelPaddingLeft + IconSize + IconPadding, _labelTop, foreground);
        }

        private void DrawCard(CanvasDrawingSession session, PaintEntity entity, CanvasBitmap photo)
        {
            var width = Size.X;
            var text = entity.LinkDark ? Colors.White : CardText;
            var accent = entity.LinkAccentColor;

            session.FillRoundedRectangle(0, 0, width, Size.Y, CardRadius, CardRadius, entity.LinkDark ? CardDark : Colors.White);

            var y = 7.33f;

            if (entity.LinkTextAbove)
            {
                session.DrawTextLayout(_message, 10, y, CardLink);
                y += (float)_message.LayoutBounds.Height + 7;
            }

            var top = y;
            var block = new Rect(10, top, width - 20, _previewHeight);

            using (var shape = CanvasGeometry.CreateRoundedRectangle(session, block, 5, 5))
            {
                session.FillGeometry(shape, Color.FromArgb(0x1A, accent.R, accent.G, accent.B));

                using (session.CreateLayer(1, shape))
                {
                    session.FillRectangle(10, top, 3, _previewHeight, accent);
                }
            }

            y += 5.66f;

            if (_siteName != null)
            {
                session.DrawTextLayout(_siteName, 20, y, accent);
                y += (float)_siteName.LayoutBounds.Height + 2.66f;
            }

            if (_title != null)
            {
                session.DrawTextLayout(_title, 20, y, text);
                y += (float)_title.LayoutBounds.Height + 2.66f;
            }

            if (_description != null)
            {
                session.DrawTextLayout(_description, 20, y, text);
                y += (float)_description.LayoutBounds.Height + 2.66f;
            }

            if (_hasPhoto && photo != null)
            {
                var rect = _smallPhoto
                    ? new Rect(width - 16 - PhotoSide, top + 6, PhotoSide, PhotoSide)
                    : new Rect(20, y + 2.66f, width - 40, _photoHeight);

                DrawPhoto(session, photo, rect);
            }

            if (!entity.LinkTextAbove)
            {
                session.DrawTextLayout(_message, 10, top + _previewHeight + 12, CardLink);
            }
        }

        // Filled, cropped about its middle, with rounded corners.
        private static void DrawPhoto(CanvasDrawingSession session, CanvasBitmap photo, Rect rect)
        {
            var source = photo.Size;
            var scale = Math.Max(rect.Width / source.Width, rect.Height / source.Height);
            var width = rect.Width / scale;
            var height = rect.Height / scale;

            var crop = new Rect((source.Width - width) / 2, (source.Height - height) / 2, width, height);

            using var clip = CanvasGeometry.CreateRoundedRectangle(session, rect, PhotoRadius, PhotoRadius);
            using (session.CreateLayer(1, clip))
            {
                session.DrawImage(photo, rect, crop);
            }
        }

        private static string WithoutScheme(string url)
        {
            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url.Substring(8)
                : url;
        }

        private static CanvasTextLayout CreateLine(ICanvasResourceCreator creator, string text, string family, FontWeight weight, FontStretch stretch, float size, float maxWidth)
        {
            using var format = new CanvasTextFormat
            {
                FontFamily = family,
                FontWeight = weight,
                FontStretch = stretch,
                FontSize = size,
                WordWrapping = CanvasWordWrapping.NoWrap,
                TrimmingGranularity = CanvasTextTrimmingGranularity.Character,
                TrimmingSign = CanvasTrimmingSign.Ellipsis
            };

            return new CanvasTextLayout(creator, text, format, Math.Max(1, maxWidth), float.PositiveInfinity);
        }

        // At most as many lines as given, the last one ending in an ellipsis. Cut by hand: trimming
        // by height is not something DirectWrite does for wrapped text.
        private static CanvasTextLayout CreateParagraph(ICanvasResourceCreator creator, string text, float size, float maxWidth, int lines)
        {
            using var format = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = size,
                WordWrapping = CanvasWordWrapping.Wrap
            };

            maxWidth = Math.Max(1, maxWidth);

            var layout = new CanvasTextLayout(creator, text, format, maxWidth, float.PositiveInfinity);
            if (layout.LineCount <= lines)
            {
                return layout;
            }

            var length = 0;

            for (int i = 0; i < lines; i++)
            {
                length += layout.LineMetrics[i].CharacterCount;
            }

            // The ellipsis takes room, so the cut moves back until the lines hold it too.
            while (length > 0 && layout.LineCount > lines)
            {
                layout.Dispose();
                layout = new CanvasTextLayout(creator, text.Substring(0, length).TrimEnd() + "…", format, maxWidth, float.PositiveInfinity);

                length--;
            }

            return layout;
        }

        public void Dispose()
        {
            _label?.Dispose();
            _icon?.Dispose();
            _message?.Dispose();
            _siteName?.Dispose();
            _title?.Dispose();
            _description?.Dispose();
        }
    }
}
