//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;

namespace Telegram.Entities
{
    public enum ImageRotation
    {
        None,
        Clockwise90Degrees,
        Clockwise180Degrees,
        Clockwise270Degrees
    }

    public enum ImageFlip
    {
        None,
        Horizontal,
        Vertical
    }

    public enum PaintEntityType
    {
        Text,

        /// <summary>
        /// A sticker or a custom emoji.
        /// </summary>
        Sticker,

        /// <summary>
        /// A GIF, which goes into the photo like a sticker but is not one.
        /// </summary>
        Animation,

        Shape,

        /// <summary>
        /// A story's suggested reaction, which viewers draw from its area.
        /// </summary>
        Reaction,

        /// <summary>
        /// A story's weather, which viewers draw from its area.
        /// </summary>
        Weather,

        /// <summary>
        /// A story's link, drawn into the story, with an area over it that opens the link.
        /// </summary>
        Link,

        /// <summary>
        /// A story's venue, drawn into the story, with an area over it that opens the map.
        /// </summary>
        Location,

        /// <summary>
        /// A photo from the device, placed over a story like a sticker.
        /// </summary>
        Photo
    }

    /// <summary>
    /// The fills a story widget can take, in the order a click cycles through them.
    /// </summary>
    public enum PaintWidgetStyle
    {
        Black,
        Translucent,
        White
    }

    public enum PaintShapeKind
    {
        Circle,
        Rectangle,
        Star,
        Bubble,
        Arrow
    }

    /// <summary>
    /// The looks a text can take, in the order its frame button cycles through them.
    /// </summary>
    public enum PaintTextFrame
    {
        Filled,
        Translucent,
        Solid,
        None
    }

    public enum PaintTextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum PaintTextFont
    {
        Regular,
        Italic,
        Serif,
        Condensed,
        Mono,
        Display
    }

    /// <summary>
    /// An object placed over the image. Like strokes it is stored against the upright image, so
    /// cropping or turning the image later never moves it.
    /// </summary>
    public partial class PaintEntity
    {
        public PaintEntityType Type { get; set; }

        /// <summary>
        /// The centre, as fractions of the upright image's size.
        /// </summary>
        public Vector2 Position { get; set; }

        public float Scale { get; set; } = 1;

        /// <summary>
        /// Clockwise, in radians, against the upright image.
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Flipped by the person, which only a sticker or a reaction can be. A reaction's bubble
        /// turns its tail to the other side, and the reaction itself stays as it is.
        /// </summary>
        public bool Mirror { get; set; }

        /// <summary>
        /// A sticker's or a GIF's file. Never serialized: the photo may be generated after a
        /// restart, when only <see cref="StickerRemoteId"/> still names it.
        /// </summary>
        [JsonIgnore]
        public File StickerFile { get; set; }

        public string StickerRemoteId { get; set; }

        /// <summary>
        /// A sticker's, a GIF's or a photo's size, of which only the shape counts.
        /// </summary>
        public int StickerWidth { get; set; }

        public int StickerHeight { get; set; }

        /// <summary>
        /// Drawn white, as a custom emoji meant to take the text's colour is.
        /// </summary>
        public bool StickerTinted { get; set; }

        public PaintShapeKind Shape { get; set; }

        public bool ShapeFilled { get; set; }

        /// <summary>
        /// The line's weight, on the drawing tools' scale.
        /// </summary>
        public float ShapeWeight { get; set; }

        // A shape's geometry is in the renderer's units around the object's position, which is
        // its origin rather than the middle of what it covers.

        /// <summary>
        /// Half the width and the height of a circle, a rectangle, a star or a bubble.
        /// </summary>
        public Vector2 ShapeSize { get; set; }

        /// <summary>
        /// Where a bubble's tail points.
        /// </summary>
        public Vector2 ShapeTail { get; set; }

        /// <summary>
        /// An arrow runs from its start, curving towards its bend, to its tip.
        /// </summary>
        public Vector2 ArrowStart { get; set; }

        public Vector2 ArrowBend { get; set; }

        public Vector2 ArrowTip { get; set; }

        public float ArrowHead { get; set; }

        public string Text { get; set; }

        public Color Color { get; set; }

        public PaintTextFrame Frame { get; set; }

        public PaintTextAlignment Alignment { get; set; }

        public PaintTextFont Font { get; set; }

        /// <summary>
        /// A multiple of the default size, from 0.5 to 2. Unlike <see cref="Scale"/> it changes
        /// where the text wraps.
        /// </summary>
        public float TextSize { get; set; } = 1;

        /// <summary>
        /// A reaction's emoji, or null for a custom emoji.
        /// </summary>
        public string ReactionEmoji { get; set; }

        public long ReactionCustomEmojiId { get; set; }

        /// <summary>
        /// A reaction on a translucent black bubble rather than a white one.
        /// </summary>
        public bool ReactionDark { get; set; }

        public ReactionType GetReactionType()
        {
            return ReactionCustomEmojiId != 0
                ? new ReactionTypeCustomEmoji(ReactionCustomEmojiId)
                : new ReactionTypeEmoji(ReactionEmoji);
        }

        public void SetReactionType(ReactionType reaction)
        {
            ReactionEmoji = (reaction as ReactionTypeEmoji)?.Emoji;
            ReactionCustomEmojiId = (reaction as ReactionTypeCustomEmoji)?.CustomEmojiId ?? 0;
        }

        public PaintWidgetStyle WidgetStyle { get; set; }

        /// <summary>
        /// A widget's size as measured when it was placed, in its own units: a 240th of the image's
        /// width at a scale of one.
        /// </summary>
        public Vector2 WidgetSize { get; set; }

        public string WeatherEmoji { get; set; }

        /// <summary>
        /// In degrees Celsius.
        /// </summary>
        public double WeatherTemperature { get; set; }

        public string LinkUrl { get; set; }

        /// <summary>
        /// Shown in place of the link, or null for the link itself.
        /// </summary>
        public string LinkName { get; set; }

        /// <summary>
        /// Shown as a card with the page's preview, rather than as a label. The preview is kept
        /// here as it was when placed, so that drawing the story needs nothing fetched.
        /// </summary>
        public bool LinkHasPreview { get; set; }

        public string LinkSiteName { get; set; }

        public string LinkTitle { get; set; }

        public string LinkDescription { get; set; }

        /// <summary>
        /// The preview's photo. Never serialized, as a sticker's file is not.
        /// </summary>
        [JsonIgnore]
        public File LinkPhotoFile { get; set; }

        public string LinkPhotoRemoteId { get; set; }

        public int LinkPhotoWidth { get; set; }

        public int LinkPhotoHeight { get; set; }

        public bool LinkLargePhoto { get; set; }

        /// <summary>
        /// The link above the preview rather than under it.
        /// </summary>
        public bool LinkTextAbove { get; set; } = true;

        /// <summary>
        /// The card on a dark fill rather than a white one.
        /// </summary>
        public bool LinkDark { get; set; }

        /// <summary>
        /// The colour the preview's site name and bar take: the poster's own.
        /// </summary>
        public Color LinkAccentColor { get; set; }

        /// <summary>
        /// The venue's name.
        /// </summary>
        public string LocationTitle { get; set; }

        public double LocationLatitude { get; set; }

        public double LocationLongitude { get; set; }

        /// <summary>
        /// The venue bot's inline query that found the venue, and its result in it, which the
        /// story area names it by.
        /// </summary>
        public long LocationQueryId { get; set; }

        public string LocationResultId { get; set; }

        /// <summary>
        /// The photo's upright copy in the app's own folders, which the story is drawn from
        /// whenever it is generated.
        /// </summary>
        public string PhotoPath { get; set; }

        public PaintEntity Clone()
        {
            return (PaintEntity)MemberwiseClone();
        }
    }

    public partial class ImageGeneration
    {
        public Rect Rectangle { get; set; } = new Rect(0, 0, 1, 1);
        public BitmapProportions Proportions { get; set; } = BitmapProportions.Custom;

        public int MinimumSize { get; set; }

        public IReadOnlyList<SmoothPathBuilder> Strokes { get; set; }

        public IReadOnlyList<PaintEntity> Entities { get; set; }

        public ImageRotation Rotation { get; set; }
        public ImageFlip Flip { get; set; }

        /// <summary>
        /// Free rotation in degrees, clockwise, applied after <see cref="Rotation"/> and
        /// <see cref="Flip"/>. <see cref="Rectangle"/> is then the crop before this rotation,
        /// turning about its own centre, so it may reach outside 0..1.
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// A story's frame, the size it is posted at, or empty for none. <see cref="Rectangle"/> is
        /// then the frame over the media and may reach outside 0..1 on any side, and the strokes
        /// and objects are laid out in the frame rather than on the media.
        /// </summary>
        public Size FrameSize { get; set; }

        /// <summary>
        /// The frame's fill where the media does not cover it, a vertical gradient.
        /// </summary>
        public Color FrameTopColor { get; set; }

        public Color FrameBottomColor { get; set; }

        public bool HasFrame => FrameSize.Width > 0 && FrameSize.Height > 0;

        /// <summary>
        /// Whether the photo has to be drawn by Win2D, which alone renders a free angle, objects
        /// and a frame.
        /// </summary>
        public bool RequiresRender => Angle != 0 || Entities is { Count: > 0 } || HasFrame;

        public TimeSpan TrimStartTime { get; set; }
        public TimeSpan TrimStopTime { get; set; }

        public bool IsEmpty
        {
            get => (Rectangle.IsEmpty || (Rectangle.X == 0 && Rectangle.Y == 0 && Rectangle.Width == 1 && Rectangle.Height == 1))
                && Strokes == null
                && Entities == null
                && Rotation == ImageRotation.None
                && Flip == ImageFlip.None
                && Angle == 0
                && !HasFrame;
        }
    }
}
