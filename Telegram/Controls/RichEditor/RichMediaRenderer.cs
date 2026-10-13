//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// What a media block shows in the editor: a still, a row for a file, never playback.
    /// Playing in the editor is the sent message's job, and a document being written should not
    /// decode video to stay on screen.
    /// </summary>
    internal sealed class RichMediaRenderer
    {
        private const double MaxHeight = 360;
        private const double FileHeight = 48;

        // What a map is drawn at most, as LocationContent draws it.
        private const double MapWidth = 320;
        private const double MapHeight = 200;
        private const double MosaicWidth = 480;

        private readonly RichEditorView _owner;

        public RichMediaRenderer(RichEditorView owner)
        {
            _owner = owner;
        }

        public void Show(RichMediaParts parts, RichMediaBlock block)
        {
            if (parts.Media == null)
            {
                return;
            }

            var source = block.Source;

            if (ReferenceEquals(parts.Shown, source))
            {
                return;
            }

            parts.Shown = source;
            parts.ShownItems.Clear();

            ClearTiles(parts);
            SetPaging(parts, false);

            // Only photos and videos go into a collage, so only they get the controls.
            var visual = source.Kind is RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation;
            SetControls(parts, visual, visual, null);

            // A picture added as a file is a file: shown as one, as it is sent as one.
            if (source.Kind is RichMediaKind.Audio or RichMediaKind.VoiceNote or RichMediaKind.Document)
            {
                ShowFile(parts, source);
                return;
            }

            HideFile(parts);
            SetBadge(parts, Describe(source));
            SetSize(parts.Media, source.Width, source.Height);
            parts.Media.HorizontalAlignment = HorizontalAlignment.Center;

            SetThumbnail(parts.Thumbnail, source);
        }

        /// <summary>
        /// A collage is tiled the way a chat album is, by the same layout, so what is seen while
        /// writing is what is sent; every tile is a photo that can be selected on its own. A
        /// slideshow shows one slide, the arrows turn it.
        /// </summary>
        public void Show(RichMediaParts parts, RichMediaGroupBlock group, int page)
        {
            if (parts.Media == null)
            {
                return;
            }

            if (IsShown(parts, group, page))
            {
                RegisterShown(parts, group, page);
                return;
            }

            parts.Shown = group;
            parts.ShownPage = page;
            parts.ShownSlideshow = group.IsSlideshow;
            parts.ShownItems.Clear();

            foreach (var item in group.Items)
            {
                parts.ShownItems.Add((item as RichMediaBlock)?.Source);
            }

            HideFile(parts);
            _owner.UnregisterItems(group);

            parts.Media.HorizontalAlignment = HorizontalAlignment.Center;

            if (group.Items.Count == 0)
            {
                parts.Thumbnail.Source = null;
                ClearTiles(parts);
                SetPaging(parts, false);
                SetControls(parts, false, true, group);
                return;
            }

            if (group.IsSlideshow || group.Items.Count == 1 || parts.Tiles == null)
            {
                SetControls(parts, true, true, group);

                page = Math.Clamp(page, 0, group.Items.Count - 1);
                var current = (RichMediaBlock)group.Items[page];

                SetBadge(parts, group.IsSlideshow
                    ? string.Format("{0} · {1}/{2}", Strings.AccDescrIVSlideshow, page + 1, group.Items.Count)
                    : Strings.AccDescrCollage);

                SetSize(parts.Media, current.Source.Width, current.Source.Height);
                SetThumbnail(parts.Thumbnail, current.Source);
                ClearTiles(parts);
                SetPaging(parts, group.IsSlideshow && group.Items.Count > 1);

                _owner.RegisterItem(current, parts.Media);
                return;
            }

            SetPaging(parts, false);
            SetBadge(parts, string.Format("{0} · {1}", Strings.AccDescrCollage, group.Items.Count));
            parts.Thumbnail.Source = null;

            var sizes = new List<Size>(group.Items.Count);
            foreach (var item in group.Items)
            {
                var source = (item as RichMediaBlock)?.Source;
                sizes.Add(source != null && source.Width > 0 && source.Height > 0
                    ? new Size(source.Width, source.Height)
                    : new Size(1, 1));
            }

            var (positions, size) = MosaicAlbumLayout.chatMessageBubbleMosaicLayout(MosaicWidth, MaxHeight, sizes);

            SetControls(parts, false, true, group);

            parts.Media.SetExtent(size.Width, size.Height);

            var children = parts.Tiles.Children;
            var count = Math.Min(positions.Length, group.Items.Count);

            for (int i = 0; i < count; i++)
            {
                ImageView tile;
                if (i < children.Count)
                {
                    tile = (ImageView)children[i];
                }
                else
                {
                    tile = new ImageView
                    {
                        Stretch = Stretch.UniformToFill
                    };

                    children.Add(tile);
                }

                var item = (RichMediaBlock)group.Items[i];
                SetThumbnail(tile, item.Source);
                SetTileControl(parts, i, item);

                _owner.RegisterItem(item, tile);
            }

            while (children.Count > count)
            {
                children.RemoveAt(children.Count - 1);
            }

            parts.Tiles.SetLayout(positions, count, size);
            parts.TileControls?.SetLayout(positions, count, size);

            TrimTileControls(parts, count);
        }

        /// <summary>The same collage, its items the same files in the same order, on the same page.</summary>
        private static bool IsShown(RichMediaParts parts, RichMediaGroupBlock group, int page)
        {
            if (!ReferenceEquals(parts.Shown, group) || parts.ShownSlideshow != group.IsSlideshow || parts.ShownItems.Count != group.Items.Count)
            {
                return false;
            }

            if (group.IsSlideshow && parts.ShownPage != page)
            {
                return false;
            }

            for (int i = 0; i < group.Items.Count; i++)
            {
                if (!ReferenceEquals(parts.ShownItems[i], (group.Items[i] as RichMediaBlock)?.Source))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The tiles drawn last time, registered again: the editor rebuilds that registry on every pass.</summary>
        private void RegisterShown(RichMediaParts parts, RichMediaGroupBlock group, int page)
        {
            if (group.IsSlideshow || group.Items.Count == 1 || parts.Tiles == null || parts.Tiles.Children.Count == 0)
            {
                if (group.Items.Count > 0)
                {
                    _owner.RegisterItem(group.Items[Math.Clamp(page, 0, group.Items.Count - 1)], parts.Media);
                }

                return;
            }

            var children = parts.Tiles.Children;
            for (int i = 0; i < children.Count && i < group.Items.Count; i++)
            {
                _owner.RegisterItem(group.Items[i], (FrameworkElement)children[i]);
            }
        }

        public void Show(RichMediaParts parts, RichMapBlock map)
        {
            if (parts.Media == null)
            {
                return;
            }

            // The location object is replaced, never changed in place, so identity is enough.
            var key = (map.Location, map.Zoom, map.Width, map.Height);
            if (parts.Shown is ValueTuple<Location, int, int, int> shown && ReferenceEquals(shown.Item1, key.Location) && shown.Item2 == key.Zoom && shown.Item3 == key.Width && shown.Item4 == key.Height)
            {
                return;
            }

            parts.Shown = key;
            parts.ShownItems.Clear();

            ClearTiles(parts);
            HideFile(parts);
            SetPaging(parts, false);
            SetControls(parts, false, false, null);
            SetBadge(parts, Strings.ArticleCommandMap);

            var fit = map.Width > 0 && map.Height > 0 ? Math.Min(1, Math.Min(MapWidth / map.Width, MapHeight / map.Height)) : 0;
            parts.Media.SetExtent(fit > 0 ? map.Width * fit : MapWidth, fit > 0 ? map.Height * fit : MapHeight);
            parts.Media.HorizontalAlignment = HorizontalAlignment.Center;

            var clientService = _owner.ClientService;
            if (clientService == null || map.Location == null)
            {
                return;
            }

            var width = Math.Clamp(map.Width, 16, 1024);
            var height = Math.Clamp(map.Height, 16, 1024);
            var scale = (int)Math.Clamp(Math.Round(_owner.XamlRoot?.RasterizationScale ?? 1), 1, 3);
            var thumbnail = parts.Thumbnail;

            clientService.Send(new GetMapThumbnailFile(map.Location, Math.Clamp(map.Zoom, 13, 20), width, height, scale, 0), result =>
            {
                if (result is File file)
                {
                    _owner.BeginOnUIThread(() => thumbnail.SetSource(clientService, file));
                }
            });
        }

        private void ClearTiles(RichMediaParts parts)
        {
            parts.Tiles?.Children.Clear();
            TrimTileControls(parts, 0);
        }

        private static void SetControls(RichMediaParts parts, bool more, bool add, RichMediaGroupBlock group)
        {
            if (parts.MoreButton == null)
            {
                return;
            }

            parts.MoreButton.Visibility = more ? Visibility.Visible : Visibility.Collapsed;
            parts.AddButton.Visibility = add ? Visibility.Visible : Visibility.Collapsed;
            parts.LayoutButton.Visibility = group != null ? Visibility.Visible : Visibility.Collapsed;

            if (group != null)
            {
                // Named for the layout it switches to, as TDesktop's is.
                parts.LayoutButton.Content = group.IsSlideshow ? Icons.Apps : Icons.Image;
                AutomationProperties.SetName(parts.LayoutButton, group.IsSlideshow ? Strings.AccDescrCollage : Strings.AccDescrIVSlideshow);
            }
        }

        private void SetTileControl(RichMediaParts parts, int index, RichMediaBlock item)
        {
            if (parts.TileControls == null)
            {
                return;
            }

            var children = parts.TileControls.Children;

            RichMediaControlButton button;
            if (index < children.Count)
            {
                button = (RichMediaControlButton)children[index];
            }
            else
            {
                button = new RichMediaControlButton
                {
                    Content = Icons.MoreHorizontal
                };

                AutomationProperties.SetName(button, Strings.AccDescrMoreOptions);
                button.Click += OnTileMoreClick;
                children.Add(button);
            }

            button.Tag = item;
        }

        private void TrimTileControls(RichMediaParts parts, int count)
        {
            if (parts.TileControls == null)
            {
                return;
            }

            var children = parts.TileControls.Children;
            while (children.Count > count)
            {
                if (children[children.Count - 1] is Button button)
                {
                    button.Click -= OnTileMoreClick;
                }

                children.RemoveAt(children.Count - 1);
            }
        }

        private void OnTileMoreClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: RichBlock item } button)
            {
                _owner.RequestMediaMenu(item, button);
            }
        }

        private static void SetPaging(RichMediaParts parts, bool visible)
        {
            if (parts.PreviousButton == null || parts.NextButton == null)
            {
                return;
            }

            parts.PreviousButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            parts.NextButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SetBadge(RichMediaParts parts, string text)
        {
            if (parts.BadgeText != null)
            {
                parts.BadgeText.Text = text ?? string.Empty;
            }

            if (parts.Badge != null)
            {
                parts.Badge.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static void ShowFile(RichMediaParts parts, RichMediaSource source)
        {
            parts.Thumbnail.Source = null;
            parts.Media.SetExtent(double.NaN, FileHeight);
            parts.Media.HorizontalAlignment = HorizontalAlignment.Left;
            parts.IsFile = true;

            SetBadge(parts, null);

            if (parts.Backdrop != null)
            {
                parts.Backdrop.Visibility = Visibility.Collapsed;
            }

            if (parts.FileRow == null)
            {
                return;
            }

            parts.FileRow.Visibility = Visibility.Visible;
            parts.FileIcon.Text = source.Kind switch
            {
                RichMediaKind.Audio => Icons.MusicNote2Filled16,
                RichMediaKind.VoiceNote => Icons.MicOnFilled24,
                _ => Icons.DocumentFilled24
            };

            GetFileText(source, out string title, out string subtitle);
            parts.FileTitle.Text = title ?? string.Empty;
            parts.FileSubtitle.Text = subtitle ?? string.Empty;
        }

        private static void HideFile(RichMediaParts parts)
        {
            parts.IsFile = false;

            if (parts.FileRow != null)
            {
                parts.FileRow.Visibility = Visibility.Collapsed;
            }

            if (parts.Backdrop != null)
            {
                parts.Backdrop.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// What a file row says: a track by its title and performer, anything else by its name,
        /// and its size or length below - from the sent message, the picked file, or the input
        /// object a track from the music picker comes as.
        /// </summary>
        private static void GetFileText(RichMediaSource source, out string title, out string subtitle)
        {
            var duration = source.Duration > 0 ? Formatter.Duration(source.Duration) : null;

            switch (source.Remote)
            {
                case PageBlockAudio x:
                    title = string.IsNullOrEmpty(x.Audio.Title) ? x.Audio.FileName : x.Audio.Title;
                    subtitle = Join(x.Audio.Performer, duration);
                    return;
                case PageBlockVoiceNote:
                    title = Strings.AttachAudio;
                    subtitle = duration;
                    return;
                case PageBlockDocument x:
                    title = x.Document.FileName;
                    subtitle = FileSizeConverter.Convert(x.Document.DocumentValue.Size);
                    return;
            }

            switch (source.Local)
            {
                case StorageAudio audio:
                    title = string.IsNullOrEmpty(audio.Title) ? audio.File.Name : audio.Title;
                    subtitle = Join(audio.Performer, duration);
                    return;
                case StorageMedia media:
                    title = media.File.Name;
                    subtitle = Join(FileSizeConverter.Convert((long)media.Size), duration);
                    return;
            }

            if (source.Input is InputAudio input)
            {
                title = input.Title;
                subtitle = Join(input.Performer, duration);
                return;
            }

            title = source.Kind == RichMediaKind.VoiceNote ? Strings.AttachAudio : Strings.AttachDocument;
            subtitle = duration;
        }

        private static string Join(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
            {
                return second;
            }

            return string.IsNullOrEmpty(second) ? first : first + " · " + second;
        }

        private static string Describe(RichMediaSource source)
        {
            var kind = source.Kind switch
            {
                RichMediaKind.Photo => Strings.AttachPhoto,
                RichMediaKind.Video => Strings.AttachVideo,
                RichMediaKind.Animation => Strings.AttachGif,
                _ => Strings.AttachDocument
            };

            return source.Duration > 0
                ? string.Format("{0} · {1}", kind, Formatter.Duration(source.Duration))
                : kind;
        }

        private static void SetSize(RichMediaFrame media, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                media.SetExtent(double.NaN, 200);
                return;
            }

            // At most as tall as MaxHeight, keeping the ratio; the frame narrows it further to
            // the column.
            var ratio = (double)width / height;
            var target = Math.Min(MaxHeight, height);

            media.SetExtent(target * ratio, target);
        }

        private void SetThumbnail(ImageView thumbnail, RichMediaSource source)
        {
            if (thumbnail == null)
            {
                return;
            }

            var clientService = _owner.ClientService;

            if (source.Preview is ImageSource preview)
            {
                thumbnail.Source = preview;
                return;
            }

            if (source.LocalPath != null)
            {
                thumbnail.Source = UriEx.ToBitmap(source.LocalPath, 0, (int)MaxHeight);
                return;
            }

            switch (source.Remote)
            {
                case PageBlockPhoto photo:
                    thumbnail.SetSource(clientService, PickSize(photo.Photo)?.Photo, photo.Photo.Minithumbnail, 0, 0);
                    break;
                case PageBlockVideo video:
                    thumbnail.SetSource(clientService, video.Video.Thumbnail?.File, video.Video.Minithumbnail, 0, 0);
                    break;
                case PageBlockAnimation animation:
                    thumbnail.SetSource(clientService, animation.Animation.Thumbnail?.File, animation.Animation.Minithumbnail, 0, 0);
                    break;
                default:
                    thumbnail.Source = null;
                    break;
            }
        }

        /// <summary>The smallest size of a photo that still fills the block at its height.</summary>
        private static PhotoSize PickSize(Photo photo)
        {
            PhotoSize best = null;

            foreach (var size in photo.Sizes)
            {
                best = size;

                if (size.Height >= MaxHeight)
                {
                    break;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// The box media is shown in: at the size it was given, or narrower at the same ratio when
    /// the column is. Given a Width and a Height instead, the part past the column was clipped
    /// while the selection and the controls still went by the whole box.
    /// </summary>
    public sealed partial class RichMediaFrame : Grid
    {
        private Size _extent;

        /// <summary>
        /// The size to show at. With no <paramref name="width"/>, a row as tall as
        /// <paramref name="height"/> and as wide as what is in it - a file.
        /// </summary>
        public void SetExtent(double width, double height)
        {
            if (double.IsNaN(width))
            {
                _extent = default;
                Width = double.NaN;
                Height = height;
            }
            else
            {
                _extent = new Size(width, height);
                Width = double.NaN;
                Height = double.NaN;
            }

            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_extent.Width <= 0 || _extent.Height <= 0)
            {
                return base.MeasureOverride(availableSize);
            }

            var scale = double.IsInfinity(availableSize.Width) ? 1 : Math.Min(1, availableSize.Width / _extent.Width);
            var size = new Size(_extent.Width * scale, _extent.Height * scale);

            base.MeasureOverride(size);
            return size;
        }
    }

    /// <summary>
    /// A collage's tiles at the album layout's rectangles, scaled to the width the frame was
    /// given, with a hairline between them. With <see cref="IsCorners"/>, each child sits at the
    /// top left corner of its tile at its own size instead, and only on a tile that can hold it.
    /// </summary>
    public sealed partial class RichCollagePanel : Panel
    {
        private readonly List<Rect> _rects = new();
        private Size _extent;

        public bool IsCorners { get; set; }

        public double CornerInset { get; set; }

        /// <summary>The smallest tile, as drawn, a corner child is shown on.</summary>
        public double CornerMinimum { get; set; }

        public void SetLayout((Rect, MosaicItemPosition)[] positions, int count, Size extent)
        {
            _rects.Clear();

            for (int i = 0; i < count; i++)
            {
                _rects.Add(positions[i].Item1);
            }

            _extent = extent;
            InvalidateMeasure();
        }

        private double Scale(double width)
        {
            return _extent.Width > 0 && !double.IsInfinity(width) ? Math.Min(1, width / _extent.Width) : 1;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var scale = Scale(availableSize.Width);
            var children = Children;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (i >= _rects.Count)
                {
                    child.Measure(default);
                    continue;
                }

                var rect = _rects[i];

                if (IsCorners)
                {
                    // Set here rather than when the layout is, as only now is the scale known.
                    var visibility = rect.Width * scale >= CornerMinimum && rect.Height * scale >= CornerMinimum ? Visibility.Visible : Visibility.Collapsed;
                    if (child.Visibility != visibility)
                    {
                        child.Visibility = visibility;
                    }

                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                }
                else
                {
                    child.Measure(new Size(Math.Max(0, rect.Width * scale - 2), Math.Max(0, rect.Height * scale - 2)));
                }
            }

            return new Size(_extent.Width * scale, _extent.Height * scale);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var scale = Scale(finalSize.Width);
            var children = Children;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (i >= _rects.Count)
                {
                    child.Arrange(default);
                    continue;
                }

                var rect = _rects[i];
                var x = rect.X * scale + 1;
                var y = rect.Y * scale + 1;

                if (IsCorners)
                {
                    child.Arrange(new Rect(x + CornerInset, y + CornerInset, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else
                {
                    child.Arrange(new Rect(x, y, Math.Max(0, rect.Width * scale - 2), Math.Max(0, rect.Height * scale - 2)));
                }
            }

            return finalSize;
        }
    }
}
