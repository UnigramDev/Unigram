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
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    /// <summary>
    /// A row of thumbnails of every item in the album the gallery is showing. The selected
    /// item is drawn at its own aspect ratio and the others as narrow slices of theirs, so the
    /// strip keeps one width however the album is laid out; clicking a thumbnail opens it.
    /// </summary>
    /// <remarks>
    /// The strip moves on the compositor, from the same progress that moves the carousel: each
    /// thumbnail's position, width and opacity are expressions of it, so the strip follows a
    /// finger, a touchpad, inertia and the carousel's own animation frame by frame, with no
    /// work on the UI thread while it moves. Layout runs only when the album or the selection
    /// changes, to work out where everything goes at rest and next to either neighbour.
    /// A jump further than a neighbour, which the carousel doesn't animate, eases in on the
    /// compositor as well.
    /// </remarks>
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

        // Progress of the carousel: -1 at the previous item, 0 at the selected one, 1 at the
        // next one. Until the carousel hands over its own, one that stays at 0.
        private readonly CompositionPropertySet _restingProgress;
        private CompositionPropertySet _progress;

        // How far a jump has got, from 0 to 1; 1 when there is none.
        private readonly CompositionPropertySet _jump;
        private readonly Stopwatch _jumpStopwatch = new();

        private long _albumId;
        private int _selectedIndex = -1;

        // Everything shrinks by this much when a large album doesn't fit the space it's given.
        // Worked out from the widest the strip can be, so it holds still while the selection moves.
        private double _scale = 1;

        public GalleryAlbumStrip()
        {
            _compositor = BootStrapper.Current.Compositor;

            _restingProgress = _compositor.CreatePropertySet();
            _restingProgress.InsertScalar("Progress", 0);
            _progress = _restingProgress;

            _jump = _compositor.CreatePropertySet();
            _jump.InsertScalar("T", 1);

            Visibility = Visibility.Collapsed;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<GalleryMedia> ItemClick;

        /// <summary>
        /// The property set whose "Progress" scalar says where the carousel is between the
        /// selected item and its neighbours, from -1 (the previous one) to 1 (the next one).
        /// </summary>
        public void SetProgressSource(CompositionPropertySet progress)
        {
            progress ??= _restingProgress;

            if (_progress == progress)
            {
                return;
            }

            _progress = progress;

            foreach (var thumb in _thumbs)
            {
                StartAnimations(thumb);
            }
        }

        /// <summary>
        /// Shows <paramref name="items"/> with <paramref name="selected"/> expanded, or hides the
        /// strip for fewer than two. Called again for the same album, as a page load at either
        /// end grows it, the thumbnails already there and anything in motion are left alone.
        /// </summary>
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
                // Thumbnails of items still in the album keep their image rather than loading it
                // again, which is all a page load at either end of it would otherwise do.
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

            // A neighbour is reached through the carousel, whose progress carries the strip
            // there. Anything further is a jump, which eases from what's on screen.
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

        // The images go below and the buttons above them, each in album order. Only what
        // changed goes in or out of the tree: taking a thumbnail out and putting it back
        // reloads its visual state, and can drop its image for a frame.
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

        // Where a value is for the carousel's progress, starting from where a jump left it.
        // c.{name}A is the value at rest, c.{name}P next to the previous item and c.{name}N
        // next to the next one; c.{name}F is where the last jump started.
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

            // The image is laid out at the full width it has when selected, and the clip shows
            // the middle of it: the same crop a narrower box would show.
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

            // Close enough to the cubic bezier the animation eases with.
            return 1 - Math.Pow(1 - t, 3);
        }

        private void StartJump()
        {
            // The carousel is at rest when a jump starts, so what is on screen is where the
            // last layout put things, or as far as the last jump had got towards there.
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

            // The widest the strip gets: one item as wide as it can be, every other one narrow.
            // The thumbnails are centered inside it, so the panel keeps one size whatever is
            // selected.
            var width = SelectedMaxWidth + (ItemWidth + Spacing) * (_thumbs.Count - 1);

            _scale = !double.IsInfinity(availableSize.Width) && width > availableSize.Width
                ? availableSize.Width / width
                : 1;

            // Neither images nor buttons have a size of their own: they take what they're given.
            var itemSize = new Size(ItemWidth * _scale, ItemHeight * _scale);

            foreach (var thumb in _thumbs)
            {
                thumb.Image.Measure(itemSize);
                thumb.Measure(itemSize);
            }

            return new Size(width * _scale, ItemHeight * _scale);
        }

        // Left edge, width and opacity of every thumbnail with the item at selected expanded.
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

            // A neighbour outside the album leaves the strip as it is while the carousel moves
            // there: whatever comes next isn't in it.
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

                // Outside of a jump, or for a thumbnail that wasn't there when it started, the
                // jump has nothing to ease from.
                if (!jumping || !thumb.HasFrom)
                {
                    thumb.SetFrom(1);
                }

                thumb.Image.Arrange(new Rect(0, 0, full, height));

                // Clicks land where the thumbnail is at rest: the strip ignores them while the
                // carousel is moving anyway.
                thumb.Arrange(new Rect(xa[i], 0, wa[i], height));
            }

            return finalSize;
        }

        #endregion
    }

    /// <summary>
    /// The button of one thumbnail, laid over its image at rest. The image itself is
    /// <see cref="Image"/>, which <see cref="GalleryAlbumStrip"/> moves on the compositor.
    /// </summary>
    public sealed partial class GalleryAlbumThumb : Button
    {
        // Under the image, so a thumbnail still being loaded isn't a hole in the strip.
        private static readonly Windows.UI.Color PlaceholderColor = Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);

        private readonly ImageBrush _texture;
        private readonly ThumbnailController _thumbnailController;

        private long _thumbnailToken;

        // At rest, and where the last jump started: left edge, width and opacity.
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

            // Transparent rather than null, or the pointer would go through it.
            Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
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

        /// <summary>
        /// Starts the next jump from <paramref name="progress"/> of the way between where the
        /// last one started and where things are at rest.
        /// </summary>
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
