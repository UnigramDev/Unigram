//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    // Thumbnails of the album the gallery is showing. They are animated on the compositor
    // from the carousel progress, layout only computes the resting and neighbour states.
    public sealed partial class GalleryAlbumStrip : Panel
    {
        private const double ItemHeight = 52;
        private const double ItemWidth = 28;
        private const double SelectedMaxWidth = 96;
        private const double Spacing = 3;
        private const double Radius = 4;

        private const double UnselectedOpacity = 0.6;

        private static readonly TimeSpan JumpDuration = TimeSpan.FromMilliseconds(200);

        private readonly List<GalleryAlbumThumb> _thumbs = new();

        private readonly Compositor _compositor;

        private CompositionPropertySet _progress;

        // Non-adjacent selection changes, from 0 to 1.
        private readonly CompositionPropertySet _jump;
        private readonly Stopwatch _jumpStopwatch = new();

        private long _albumId;
        private int _selectedIndex = -1;

        private double _scale = 1;

        public GalleryAlbumStrip()
        {
            _compositor = BootStrapper.Current.Compositor;

            _jump = _compositor.CreatePropertySet();
            _jump.InsertScalar("T", 1);

            Visibility = Visibility.Collapsed;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<GalleryMedia> ItemClick;

        public void SetProgressSource(CompositionPropertySet progress)
        {
            _progress = progress;
        }

        public void Update(IList<GalleryMedia> items, GalleryMedia selected)
        {
            var index = items?.IndexOf(selected) ?? -1;
            if (items == null || items.Count < 2 || index < 0)
            {
                Clear();
                return;
            }

            var sameAlbum = _thumbs.Count > 0 && _albumId == selected.MediaAlbumId;
            if (!sameAlbum)
            {
                Clear();
            }

            var previousSelected = _selectedIndex >= 0 && _selectedIndex < _thumbs.Count
                ? _thumbs[_selectedIndex].Item
                : null;

            var previous = _thumbs.ToArray();
            var changed = previous.Length != items.Count;

            _thumbs.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var thumb = Array.Find(previous, x => x.Item == items[i]);
                if (thumb == null)
                {
                    thumb = new GalleryAlbumThumb(items[i], _compositor);
                    thumb.Click += Thumb_Click;

                    StartAnimations(thumb);
                }

                changed |= i >= previous.Length || previous[i] != thumb;
                _thumbs.Add(thumb);
            }

            if (changed)
            {
                foreach (var thumb in previous)
                {
                    if (!_thumbs.Contains(thumb))
                    {
                        thumb.Click -= Thumb_Click;
                        thumb.Recycle();
                    }
                }

                UpdateChildren();
                UpdateAutomation();
            }

            var previousIndex = previousSelected != null ? _thumbs.FindIndex(x => x.Item == previousSelected) : -1;

            // Neighbours are animated by the carousel progress.
            if (sameAlbum && previousSelected != selected && (previousIndex < 0 || Math.Abs(previousIndex - index) > 1))
            {
                StartJump();
            }

            _albumId = selected.MediaAlbumId;
            _selectedIndex = index;

            Visibility = Visibility.Visible;

            InvalidateMeasure();
            InvalidateArrange();
        }

        public void Clear()
        {
            foreach (var thumb in _thumbs)
            {
                thumb.Click -= Thumb_Click;
                thumb.Recycle();
            }

            _thumbs.Clear();
            Children.Clear();

            _albumId = 0;
            _selectedIndex = -1;

            _jumpStopwatch.Reset();
            _jump.InsertScalar("T", 1);

            Visibility = Visibility.Collapsed;
        }

        // Images first, buttons on top. Only changed elements are moved, to avoid flickering.
        private void UpdateChildren()
        {
            var desired = new List<UIElement>(_thumbs.Count * 2);

            foreach (var thumb in _thumbs)
            {
                desired.Add(thumb.Image);
            }

            foreach (var thumb in _thumbs)
            {
                desired.Add(thumb);
            }

            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(Children[i]))
                {
                    Children.RemoveAt(i);
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (i < Children.Count && Children[i] == desired[i])
                {
                    continue;
                }

                Children.Remove(desired[i]);
                Children.Insert(i, desired[i]);
            }
        }

        private void UpdateAutomation()
        {
            for (int i = 0; i < _thumbs.Count; i++)
            {
                var thumb = _thumbs[i];
                var type = thumb.Item.IsVideo ? Strings.AttachVideo : Strings.AttachPhoto;

                AutomationProperties.SetName(thumb, $"{type}, {string.Format(Strings.Of, i + 1, _thumbs.Count)}");
                AutomationProperties.SetPositionInSet(thumb, i + 1);
                AutomationProperties.SetSizeOfSet(thumb, _thumbs.Count);
            }
        }

        private void Thumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is GalleryAlbumThumb thumb && _thumbs.IndexOf(thumb) != _selectedIndex)
            {
                ItemClick?.Invoke(this, thumb.Item);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        #region Composition

        // A: at rest, P/N: previous/next item selected, F: start of the current jump.
        private static string Value(string name)
        {
            var carousel = $"((pr.Progress < 0) ? (c.{name}A + (c.{name}P - c.{name}A) * -pr.Progress) : (c.{name}A + (c.{name}N - c.{name}A) * pr.Progress))";
            return $"(c.{name}F + ({carousel} - c.{name}F) * j.T)";
        }

        private void StartAnimations(GalleryAlbumThumb thumb)
        {
            var x = Value("X");
            var w = Value("W");

            ExpressionAnimation Create(string expression)
            {
                var animation = _compositor.CreateExpressionAnimation(expression);
                animation.SetReferenceParameter("pr", _progress);
                animation.SetReferenceParameter("j", _jump);
                animation.SetReferenceParameter("c", thumb.Properties);
                return animation;
            }

            // The image is laid out at full width and clipped around its center.
            thumb.Visual.StartAnimation("Translation.X", Create($"{x} + {w} / 2 - c.Full / 2"));
            thumb.Visual.StartAnimation("Opacity", Create(Value("O")));

            thumb.ClipGeometry.StartAnimation("Offset", Create($"Vector2((c.Full - {w}) / 2, 0)"));
            thumb.ClipGeometry.StartAnimation("Size", Create($"Vector2({w}, c.H)"));
        }

        private double GetJumpProgress()
        {
            if (!_jumpStopwatch.IsRunning || _jumpStopwatch.Elapsed >= JumpDuration)
            {
                return 1;
            }

            var t = _jumpStopwatch.Elapsed.TotalMilliseconds / JumpDuration.TotalMilliseconds;

            return 1 - Math.Pow(1 - t, 3);
        }

        private void StartJump()
        {
            var progress = GetJumpProgress();

            foreach (var thumb in _thumbs)
            {
                if (thumb.HasLayout)
                {
                    thumb.SetFrom(progress);
                }
            }

            // Ease out cubic.
            var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.215f, 0.61f), new Vector2(0.355f, 1));

            var animation = _compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, 0);
            animation.InsertKeyFrame(1, 1, easing);
            animation.Duration = JumpDuration;

            _jump.InsertScalar("T", 0);
            _jump.StartAnimation("T", animation);

            _jumpStopwatch.Restart();
        }

        #endregion

        #region Layout

        private static double GetSelectedWidth(GalleryAlbumThumb thumb)
        {
            var ratio = thumb.AspectRatio;
            if (ratio <= 0)
            {
                return ItemHeight;
            }

            return Math.Clamp(ItemHeight * ratio, ItemWidth, SelectedMaxWidth);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_thumbs.Count == 0)
            {
                return new Size(0, 0);
            }

            // Widest possible layout, so the size doesn't change with the selection.
            var width = SelectedMaxWidth + (ItemWidth + Spacing) * (_thumbs.Count - 1);

            _scale = !double.IsInfinity(availableSize.Width) && width > availableSize.Width
                ? availableSize.Width / width
                : 1;

            var itemSize = new Size((float)(ItemWidth * _scale), (float)(ItemHeight * _scale));

            foreach (var thumb in _thumbs)
            {
                thumb.Image.Measure(itemSize);
                thumb.Measure(itemSize);
            }

            return new Size((float)(width * _scale), (float)(ItemHeight * _scale));
        }

        private void GetLayout(int selected, double finalWidth, double[] x, double[] w, double[] o)
        {
            var total = Spacing * _scale * (_thumbs.Count - 1);

            for (int i = 0; i < _thumbs.Count; i++)
            {
                w[i] = (i == selected ? GetSelectedWidth(_thumbs[i]) : ItemWidth) * _scale;
                o[i] = i == selected ? 1 : UnselectedOpacity;

                total += w[i];
            }

            var left = (finalWidth - total) / 2;

            for (int i = 0; i < _thumbs.Count; i++)
            {
                x[i] = left;
                left += w[i] + Spacing * _scale;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var count = _thumbs.Count;
            if (count == 0 || _selectedIndex < 0 || _selectedIndex >= count)
            {
                return finalSize;
            }

            var xa = new double[count];
            var wa = new double[count];
            var oa = new double[count];
            GetLayout(_selectedIndex, finalSize.Width, xa, wa, oa);

            var xp = new double[count];
            var wp = new double[count];
            var op = new double[count];
            GetLayout(_selectedIndex > 0 ? _selectedIndex - 1 : _selectedIndex, finalSize.Width, xp, wp, op);

            var xn = new double[count];
            var wn = new double[count];
            var on = new double[count];
            GetLayout(_selectedIndex < count - 1 ? _selectedIndex + 1 : _selectedIndex, finalSize.Width, xn, wn, on);

            var height = ItemHeight * _scale;
            var jumping = GetJumpProgress() < 1;

            for (int i = 0; i < count; i++)
            {
                var thumb = _thumbs[i];
                var full = GetSelectedWidth(thumb) * _scale;

                thumb.SetLayout(xa[i], wa[i], oa[i], xp[i], wp[i], op[i], xn[i], wn[i], on[i], full, height, Radius * _scale);

                if (!jumping || !thumb.HasFrom)
                {
                    thumb.SetFrom(1);
                }

                thumb.Image.Arrange(new Rect(0, 0, (float)full, (float)height));

                thumb.Arrange(new Rect((float)xa[i], 0, (float)wa[i], (float)height));
            }

            return finalSize;
        }

        #endregion
    }

    public sealed partial class GalleryAlbumThumb : Button
    {
        private static readonly Windows.UI.Color PlaceholderColor = Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

        private readonly ImageBrush _texture;
        private readonly ThumbnailController _thumbnailController;

        private long _thumbnailToken;

        private double _x, _w, _o;
        private double _fromX, _fromW, _fromO;

        public GalleryAlbumThumb(GalleryMedia item, Compositor compositor)
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

            Background = new SolidColorBrush(Colors.Transparent);
            IsTabStop = false;

            Image = new Border
            {
                Background = new SolidColorBrush(PlaceholderColor),
                IsHitTestVisible = false,
                Child = new Border
                {
                    Background = _texture
                }
            };

            ElementCompositionPreview.SetIsTranslationEnabled(Image, true);

            Visual = ElementComposition.GetElementVisual(Image);
            ClipGeometry = compositor.CreateRoundedRectangleGeometry();
            Visual.Clip = compositor.CreateGeometricClip(ClipGeometry);

            Properties = compositor.CreatePropertySet();

            foreach (var name in new[] { "X", "W", "O" })
            {
                Properties.InsertScalar(name + "A", 0);
                Properties.InsertScalar(name + "P", 0);
                Properties.InsertScalar(name + "N", 0);
                Properties.InsertScalar(name + "F", 0);
            }

            Properties.InsertScalar("Full", 0);
            Properties.InsertScalar("H", 0);

            UpdateThumbnail(item.Thumbnail, true);
        }

        public GalleryMedia Item { get; }

        public double AspectRatio { get; }

        internal Border Image { get; }

        internal Visual Visual { get; }

        internal CompositionRoundedRectangleGeometry ClipGeometry { get; }

        internal CompositionPropertySet Properties { get; }

        internal bool HasLayout { get; private set; }

        internal bool HasFrom { get; private set; }

        internal void SetLayout(double xa, double wa, double oa, double xp, double wp, double op, double xn, double wn, double on, double full, double height, double radius)
        {
            _x = xa;
            _w = wa;
            _o = oa;

            Properties.InsertScalar("XA", (float)xa);
            Properties.InsertScalar("WA", (float)wa);
            Properties.InsertScalar("OA", (float)oa);
            Properties.InsertScalar("XP", (float)xp);
            Properties.InsertScalar("WP", (float)wp);
            Properties.InsertScalar("OP", (float)op);
            Properties.InsertScalar("XN", (float)xn);
            Properties.InsertScalar("WN", (float)wn);
            Properties.InsertScalar("ON", (float)on);
            Properties.InsertScalar("Full", (float)full);
            Properties.InsertScalar("H", (float)height);

            ClipGeometry.CornerRadius = new Vector2((float)radius);

            HasLayout = true;
        }

        internal void SetFrom(double progress)
        {
            if (HasFrom && progress < 1)
            {
                _fromX += (_x - _fromX) * progress;
                _fromW += (_w - _fromW) * progress;
                _fromO += (_o - _fromO) * progress;
            }
            else
            {
                _fromX = _x;
                _fromW = _w;
                _fromO = _o;
            }

            Properties.InsertScalar("XF", (float)_fromX);
            Properties.InsertScalar("WF", (float)_fromW);
            Properties.InsertScalar("OF", (float)_fromO);

            HasFrom = true;
        }

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
