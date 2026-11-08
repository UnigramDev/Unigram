//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    /// <summary>
    /// A row of thumbnails of every item in the album the gallery is showing. The selected
    /// item is drawn at its own aspect ratio and the others as narrow slices of theirs, so the
    /// strip keeps one width however the album is laid out; clicking a thumbnail opens it.
    /// </summary>
    public sealed partial class GalleryAlbumStrip : Panel
    {
        private const double ItemHeight = 52;
        private const double ItemWidth = 28;
        private const double SelectedMaxWidth = 96;
        private const double Spacing = 3;

        // Everything shrinks by this much when a large album doesn't fit the space it's given.
        private double _scale = 1;

        private const double UnselectedOpacity = 0.6;

        private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);

        private readonly List<GalleryAlbumThumb> _thumbs = new();

        private GalleryMedia _selected;

        // Width of each thumbnail when the animation started, and how long it has been running.
        private readonly Stopwatch _stopwatch = new();
        private double[] _from;
        private bool _animating;
        private double _progress = 1;

        public GalleryAlbumStrip()
        {
            Visibility = Visibility.Collapsed;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<GalleryMedia> ItemClick;

        /// <summary>
        /// Shows <paramref name="items"/> with <paramref name="selected"/> expanded, or hides the
        /// strip for fewer than two. The change of selection is animated only when the album on
        /// screen is the same one, possibly grown by a page load at either end.
        /// </summary>
        public void Update(IList<GalleryMedia> items, GalleryMedia selected)
        {
            if (items == null || items.Count < 2 || selected == null)
            {
                Clear();
                return;
            }

            var animate = _selected != null
                && _selected != selected
                && _thumbs.Count > 0
                && _thumbs[0].Item.MediaAlbumId == selected.MediaAlbumId;

            var from = animate ? GetWidths() : null;
            var previous = _thumbs.ToArray();

            _thumbs.Clear();
            Children.Clear();

            foreach (var item in items)
            {
                // Thumbnails of items still in the album keep their image rather than loading it
                // again, which is all a page load at either end of it would otherwise do.
                var thumb = Array.Find(previous, x => x.Item == item) ?? new GalleryAlbumThumb(item);
                thumb.Click -= Thumb_Click;
                thumb.Click += Thumb_Click;

                _thumbs.Add(thumb);
                Children.Add(thumb);
            }

            foreach (var thumb in previous)
            {
                if (!_thumbs.Contains(thumb))
                {
                    thumb.Click -= Thumb_Click;
                    thumb.Recycle();
                }
            }

            for (int i = 0; i < _thumbs.Count; i++)
            {
                var thumb = _thumbs[i];
                var type = thumb.Item.IsVideo ? Strings.AttachVideo : Strings.AttachPhoto;

                AutomationProperties.SetName(thumb, $"{type}, {string.Format(Strings.Of, i + 1, _thumbs.Count)}");
                AutomationProperties.SetPositionInSet(thumb, i + 1);
                AutomationProperties.SetSizeOfSet(thumb, _thumbs.Count);
            }

            if (animate && from != null)
            {
                // A thumbnail that just loaded starts out narrow, like the others it joins.
                _from = new double[_thumbs.Count];

                for (int i = 0; i < _thumbs.Count; i++)
                {
                    var index = Array.IndexOf(previous, _thumbs[i]);
                    _from[i] = index >= 0 && index < from.Length ? from[index] : ItemWidth;
                }

                Start();
            }
            else
            {
                Stop();
            }

            _selected = selected;
            Visibility = Visibility.Visible;

            InvalidateMeasure();
        }

        public void Clear()
        {
            Stop();

            foreach (var thumb in _thumbs)
            {
                thumb.Click -= Thumb_Click;
                thumb.Recycle();
            }

            _thumbs.Clear();
            Children.Clear();

            _selected = null;
            Visibility = Visibility.Collapsed;
        }

        private void Thumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is GalleryAlbumThumb thumb && thumb.Item != _selected)
            {
                ItemClick?.Invoke(this, thumb.Item);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        #region Layout

        private double GetTargetWidth(GalleryAlbumThumb thumb)
        {
            if (thumb.Item != _selected)
            {
                return ItemWidth;
            }

            var ratio = thumb.AspectRatio;
            if (ratio <= 0)
            {
                return ItemHeight;
            }

            return Math.Clamp(ItemHeight * ratio, ItemWidth, SelectedMaxWidth);
        }

        private double GetWidth(int index)
        {
            var target = GetTargetWidth(_thumbs[index]);

            if (_from == null || _progress >= 1 || index >= _from.Length)
            {
                return target;
            }

            return _from[index] + (target - _from[index]) * _progress;
        }

        private double[] GetWidths()
        {
            var widths = new double[_thumbs.Count];

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = GetWidth(i);
            }

            return widths;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_thumbs.Count == 0)
            {
                return new Size(0, 0);
            }

            // Taken from where the animation ends rather than from the frame being drawn, so
            // the scale holds still while the selection moves.
            var target = Spacing * (_thumbs.Count - 1);

            foreach (var thumb in _thumbs)
            {
                target += GetTargetWidth(thumb);
            }

            _scale = !double.IsInfinity(availableSize.Width) && target > availableSize.Width
                ? availableSize.Width / target
                : 1;

            var width = 0d;

            for (int i = 0; i < _thumbs.Count; i++)
            {
                var itemWidth = GetWidth(i) * _scale;
                _thumbs[i].Measure(new Size(itemWidth, ItemHeight * _scale));

                width += itemWidth;
            }

            width += Spacing * _scale * (_thumbs.Count - 1);
            return new Size(width, ItemHeight * _scale);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = 0d;

            for (int i = 0; i < _thumbs.Count; i++)
            {
                width += GetWidth(i) * _scale;
            }

            width += Spacing * _scale * Math.Max(0, _thumbs.Count - 1);

            var x = Math.Max(0, (finalSize.Width - width) / 2);

            for (int i = 0; i < _thumbs.Count; i++)
            {
                var thumb = _thumbs[i];
                var itemWidth = GetWidth(i) * _scale;

                thumb.Arrange(new Rect(x, 0, itemWidth, ItemHeight * _scale));
                thumb.Opacity = GetOpacity(thumb, i);

                x += itemWidth + Spacing * _scale;
            }

            return finalSize;
        }

        private double GetOpacity(GalleryAlbumThumb thumb, int index)
        {
            var target = thumb.Item == _selected ? 1 : UnselectedOpacity;

            if (_from == null || _progress >= 1 || index >= _from.Length)
            {
                return target;
            }

            // Follows the width: a thumbnail that is growing is the one being selected.
            var source = _from[index] > ItemWidth ? 1 : UnselectedOpacity;
            return source + (target - source) * _progress;
        }

        #endregion

        #region Animation

        private void Start()
        {
            _progress = 0;

            if (!_animating)
            {
                _animating = true;
                CompositionTarget.Rendering += OnRendering;
            }

            // Timed here rather than from the event's arguments: the CompositionTarget alias
            // used on CsWinRT doesn't promise to hand them over.
            _stopwatch.Restart();
        }

        private void Stop()
        {
            if (_animating)
            {
                _animating = false;
                CompositionTarget.Rendering -= OnRendering;
            }

            _stopwatch.Reset();
            _progress = 1;
            _from = null;
        }

        private void OnRendering(object sender, object e)
        {
            if (!_animating)
            {
                return;
            }

            var t = Math.Clamp(_stopwatch.Elapsed.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);

            // Ease out cubic.
            _progress = 1 - Math.Pow(1 - t, 3);

            if (t >= 1)
            {
                Stop();
            }

            InvalidateMeasure();
        }

        #endregion
    }

    public sealed partial class GalleryAlbumThumb : Button
    {
        private readonly ImageBrush _texture;
        private readonly ThumbnailController _thumbnailController;

        private long _thumbnailToken;

        public GalleryAlbumThumb(GalleryMedia item)
        {
            Item = item;
            AspectRatio = GetAspectRatio(item.Constraint);

            _texture = new ImageBrush
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            _thumbnailController = new ThumbnailController(_texture);

            Style = BootStrapper.Current.Resources["EmptyButtonStyle"] as Style;
            Background = null;
            CornerRadius = new CornerRadius(4);
            IsTabStop = false;

            Content = new Border
            {
                Background = _texture,
                CornerRadius = new CornerRadius(4)
            };

            UpdateThumbnail(item.Thumbnail, true);
        }

        public GalleryMedia Item { get; }

        public double AspectRatio { get; }

        public void Recycle()
        {
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);
            _thumbnailController.Recycle();
        }

        private void UpdateThumbnail(File file)
        {
            UpdateThumbnail(file, false);
        }

        private void UpdateThumbnail(File file, bool download)
        {
            var item = Item;
            var hashCode = item.GetHashCode();

            if (file != null && file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Unsubscribe(this, ref _thumbnailToken);
                _thumbnailController.Bitmap(file.Local.Path, hashCode: hashCode);
                return;
            }

            if (file != null && download)
            {
                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    item.ClientService.DownloadFile(file.Id, 1);
                }

                UpdateManager.Subscribe(this, item.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
            }

            if (item.Minithumbnail != null)
            {
                _thumbnailController.Blur(item.Minithumbnail.Data, 3, hashCode);
            }
        }

        private static double GetAspectRatio(object constraint)
        {
            static double Ratio(double width, double height)
            {
                return width > 0 && height > 0 ? width / height : 0;
            }

            return constraint switch
            {
                MessagePhoto messagePhoto => GetAspectRatio(messagePhoto.Photo),
                MessageVideo messageVideo => GetAspectRatio(messageVideo.Video),
                MessageAnimation messageAnimation => GetAspectRatio(messageAnimation.Animation),
                Photo photo when photo.GetBig() is PhotoSize big => Ratio(big.Width, big.Height),
                Video video => Ratio(video.Width, video.Height),
                Animation animation => Ratio(animation.Width, animation.Height),
                _ => 0
            };
        }
    }
}
