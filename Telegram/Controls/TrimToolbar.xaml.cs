//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    /// <summary>
    /// A video's trim range over a strip of its frames, the mute toggle beside it, and the
    /// player kept looping inside the range.
    /// </summary>
    public sealed partial class TrimToolbar : Grid
    {
        private XamlRoot _xamlRoot;
        private StorageMedia _media;
        private AsyncMediaPlayer _player;

        private TimeSpan _duration;
        private bool _resume;

        private int _thumbnailWidth;
        private int _thumbnailHeight;
        private int _thumbnailCount;

        // The window TrimZoomThumbnails holds the frames of, and which request filled it, so
        // a zoom that ended stops its decode.
        private int _zoomVersion;
        private double _zoomStart;
        private double _zoomLength = 1;

        // The cover: a dot over the track marks it. Letting go of the playhead flies the frame
        // into a preview over the dot, which shrinks into it after a moment. The preview mirrors
        // the editor's frame, and the player is held on the cover until it is gone, so nothing
        // is decoded for it.
        private const double DotSize = 9;
        private const double DotRestingSize = 5;
        private const double DotDuration = 500;

        // From the strip's top up to the dot's centre; see the XAML.
        private const double DotRise = 8.5;

        private const double BubbleSide = 62;
        private const double BubbleSkip = 8;
        private const double BubbleBorder = 2;
        private const double BubbleRadius = 4;
        private const double BubbleDuration = 500;
        private const int BubbleVisible = 600;

        private const double CaptureDuration = 420;

        private MediaEditorSurface _coverSurface;
        private bool _coverDragging;

        private CompositionVisualSurface _mirror;
        private CompositionRoundedRectangleGeometry _mirrorShape;
        private bool _mirroring;
        private Size _bubbleSize;

        private bool _bubbleVisible;
        private DispatcherTimer _bubbleTimer;

        // Where the frame flies into the bubble from, in CoverLayer.
        private Rect _captureFrom;

        private readonly Tween _dot = new();
        private readonly Tween _bubble = new();
        private readonly Tween _capture = new();
        private bool _rendering;

        private readonly ThemeShadowEx _shadow;

        public TrimToolbar()
        {
            InitializeComponent();

            _shadow = new ThemeShadowEx(Card, 16);
        }

        // Raised when the user toggles it, not when IsMuted is set.
        public event EventHandler MuteChanged;

        public bool CanMute
        {
            get => MuteButton.Visibility == Visibility.Visible;
            set => MuteButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// What a host shows beside the strip, inside the toolbar.
        /// </summary>
        public object Accessory
        {
            get => AccessoryPresenter.Content;
            set => AccessoryPresenter.Content = value;
        }

        public bool IsMuted
        {
            get => MuteButton.IsChecked == true;
            set
            {
                MuteButton.IsChecked = value;
                UpdateMute();
            }
        }

        public TimeSpan TrimStartTime => IsTrimmed
            ? TimeSpan.FromMilliseconds(TrimRange.Minimum * TrimRange.OriginalDuration.TotalMilliseconds)
            : default;

        public TimeSpan TrimStopTime => IsTrimmed
            ? TimeSpan.FromMilliseconds(TrimRange.Maximum * TrimRange.OriginalDuration.TotalMilliseconds)
            : default;

        private bool IsTrimmed => TrimRange.Minimum != 0 || TrimRange.Maximum != 1;

        /// <summary>
        /// From the start of the video, not of the trim.
        /// </summary>
        public TimeSpan CoverTime => TimeSpan.FromMilliseconds(TrimRange.Cover * _duration.TotalMilliseconds);

        /// <summary>
        /// Marks the cover over the track, and previews it from <paramref name="surface"/>'s frame.
        /// </summary>
        public void EnableCover(MediaEditorSurface surface)
        {
            _coverSurface = surface;

            FindName(nameof(CoverLayer));

            // Room over the strip for the dot.
            //var margin = LayoutRoot.Margin;
            //margin.Top = 12;
            //LayoutRoot.Margin = margin;

            var compositor = BootStrapper.Current.Compositor;

            _mirror = compositor.CreateVisualSurface();
            _mirrorShape = compositor.CreateRoundedRectangleGeometry();

            var brush = compositor.CreateSurfaceBrush(_mirror);
            brush.Stretch = CompositionStretch.Fill;

            var visual = compositor.CreateSpriteVisual();
            visual.Brush = brush;
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Clip = compositor.CreateGeometricClip(_mirrorShape);

            ElementCompositionPreview.SetElementChildVisual(CoverBubbleContent, visual);

            _dot.Jump(0);
            UpdateDot();
        }

        // The rasterization scale comes from xamlRoot: the toolbar may not be in the tree yet
        // when the first frames are decoded. maximumLength defaults to the whole video.
        public async void Initialize(XamlRoot xamlRoot, AsyncMediaPlayer player, StorageMedia media, TimeSpan? maximumLength, bool restoreTrim)
        {
            _xamlRoot = xamlRoot;
            _media = media;
            _player = player;

            if (_player != null)
            {
                _player.PositionChanged += Player_PositionChanged;
                _player.EndReached += Player_EndReached;
            }

            try
            {
                using var stream = await media.File.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

                var pixelWidth = animation.PixelWidth;
                var pixelHeight = animation.PixelHeight;

                if (animation.Rotation is 90 or 270)
                {
                    pixelWidth = animation.PixelHeight;
                    pixelHeight = animation.PixelWidth;
                }

                // A single row of frames 40 tall, each as wide as its aspect makes it, so the
                // height is what the scale is pinned to. Fitting inside 40 square instead would
                // leave a portrait video too short to fill the strip.
                double ratio = 40d / pixelHeight;

                _thumbnailWidth = (int)(pixelWidth * ratio);
                _thumbnailHeight = (int)(pixelHeight * ratio);

                var count = Math.Ceiling(296d / _thumbnailWidth);
                _thumbnailCount = (int)count;

                var duration = TimeSpan.FromMilliseconds(animation.Duration);
                var maxLength = maximumLength ?? duration;

                TrimRange.SetOriginalDuration(_duration = duration, maxLength);
                TrimThumbnails.Children.Clear();

                // A held handle zooms the track until one frame of the strip spans all of it.
                TrimRange.ZoomFactor = count;

                if (restoreTrim && media.EditState?.TrimStartTime != TimeSpan.Zero && media.EditState?.TrimStopTime != TimeSpan.Zero)
                {
                    TrimRange.Minimum = media.EditState.TrimStartTime.TotalSeconds / TrimRange.OriginalDuration.TotalSeconds;
                    TrimRange.Maximum = media.EditState.TrimStopTime.TotalSeconds / TrimRange.OriginalDuration.TotalSeconds;
                }

                UpdateLoop();

                for (int i = 0; i < count; i++)
                {
                    var image = await RenderThumbnailAsync(animation, (long)(animation.Duration / count * i), false);

                    Grid.SetColumn(image, i);

                    TrimThumbnails.Children.Add(image);
                    TrimThumbnails.ColumnDefinitions.Add(1, i == count - 1 ? GridUnitType.Star : GridUnitType.Auto);
                }
            }
            catch
            {
                // People like to delete files while they're sending them
            }
        }

        public void Close()
        {
            _zoomVersion++;

            if (_coverSurface != null)
            {
                _coverSurface = null;

                StopRendering();

                if (_bubbleTimer != null)
                {
                    _bubbleTimer.Stop();
                    _bubbleTimer.Tick -= BubbleTimer_Tick;
                }

                ElementCompositionPreview.SetElementChildVisual(CoverBubbleContent, null);

                _mirror.SourceVisual = null;
                _mirror = null;
                _mirrorShape = null;
                _mirroring = false;
            }

            if (_player != null)
            {
                _player.PositionChanged -= Player_PositionChanged;
                _player.EndReached -= Player_EndReached;
                _player = null;
            }
        }

        public bool ProcessKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers modifiers)
        {
            if (key == VirtualKey.M && modifiers == VirtualKeyModifiers.None)
            {
                if (CanMute)
                {
                    MuteButton.IsChecked = MuteButton.IsChecked != true;
                    UpdateMute();

                    MuteChanged?.Invoke(this, EventArgs.Empty);
                }
                else if (_player != null)
                {
                    _player.Mute = !_player.Mute;
                }

                return true;
            }

            return false;
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMute();
            MuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateMute()
        {
            var muted = MuteButton.IsChecked == true;
            var label = muted ? Strings.Unmute : Strings.Mute;

            AutomationProperties.SetName(MuteButton, label);
            ToolTipService.SetToolTip(MuteButton, label);

            if (_player != null)
            {
                _player.Mute = muted;
            }
        }

        private async void InitializeZoomThumbnails(int version, double start, double length)
        {
            try
            {
                using var stream = await _media.File.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

                // The cells are the strip's, each showing the moment the zoomed track draws at
                // its centre. The track draws a value at a + value * b.
                var a = TrimRange.GetPosition(0, start, length);
                var b = TrimRange.GetPosition(1, start, length) - a;

                for (int i = 0; i < _thumbnailCount; i++)
                {
                    var value = Math.Clamp(((i + 0.5) * _thumbnailWidth - a) / b, 0, 1);
                    var image = await RenderThumbnailAsync(animation, (long)(animation.Duration * value), true);

                    if (version != _zoomVersion)
                    {
                        return;
                    }

                    image.HorizontalAlignment = HorizontalAlignment.Left;
                    image.Margin = new Thickness(i * _thumbnailWidth, 0, 0, 0);

                    TrimZoomThumbnails.Children.Add(image);
                }
            }
            catch
            {
                // People like to delete files while they're sending them
            }
        }

        private async Task<Border> RenderThumbnailAsync(VideoAnimation animation, long milliseconds, bool precise)
        {
            int rasterWidth;
            int rasterHeight;
            if (animation.Rotation is 90 or 270)
            {
                rasterWidth = (int)(_thumbnailHeight * _xamlRoot.RasterizationScale);
                rasterHeight = (int)(_thumbnailWidth * _xamlRoot.RasterizationScale);
            }
            else
            {
                rasterWidth = (int)(_thumbnailWidth * _xamlRoot.RasterizationScale);
                rasterHeight = (int)(_thumbnailHeight * _xamlRoot.RasterizationScale);
            }

            var buffer = BufferSurface.Create((uint)(rasterWidth * rasterHeight * 4));

            var rendered = await Task.Run(() =>
            {
                animation.SeekToMilliseconds(milliseconds, false);

                var found = animation.RenderSync(buffer, rasterWidth, rasterHeight, true, out double seconds);

                // The seek lands on the keyframe before, and the frames a zoomed strip is for
                // are those between keyframes. Not the native precise seek: nothing uses it.
                // A failed render leaves the buffer alone, so it keeps the last frame reached.
                while (precise && found && seconds * 1000 < milliseconds)
                {
                    if (!animation.RenderSync(buffer, rasterWidth, rasterHeight, true, out seconds))
                    {
                        break;
                    }
                }

                return found;
            });

            var bitmap = new WriteableBitmap(rasterWidth, rasterHeight);

            // Otherwise the buffer holds uninitialised memory.
            if (rendered)
            {
                BufferSurface.Copy(buffer, bitmap.PixelBuffer);
            }

            var image = new Border();
            image.Height = _thumbnailHeight;
            image.Width = _thumbnailWidth;
            image.Background = new ImageBrush
            {
                ImageSource = bitmap,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.Fill,
                RelativeTransform = new CompositeTransform
                {
                    CenterX = 0.5,
                    CenterY = 0.5,
                    Rotation = animation.Rotation
                }
            };

            return image;
        }

        // AsyncMediaPlayer raises its events on the thread that created it, so this and
        // Player_EndReached are already on the UI thread.
        private void Player_PositionChanged(AsyncMediaPlayer sender, AsyncMediaPlayerPositionChangedEventArgs args)
        {
            // The duration is only known once the probe in Initialize has run, and until
            // then every position is past the end of a zero length range.
            if (TrimRange.IsChanging || _duration == TimeSpan.Zero)
            {
                return;
            }

            TrimRange.Value = args.Position / _duration.TotalSeconds;
        }

        private void Player_EndReached(AsyncMediaPlayer sender, object args)
        {
            sender.Play();

            if (TrimRange.Minimum != 0)
            {
                sender.Position = _duration.TotalSeconds * TrimRange.Minimum;
            }
        }

        // The player loops at the trim end itself, from the first frame past it, rather than once
        // a reported position is past it.
        private void UpdateLoop()
        {
            if (_player == null || _duration == TimeSpan.Zero)
            {
                return;
            }

            if (TrimRange.Minimum > 0 || TrimRange.Maximum < 1)
            {
                _player.SetLoop(_duration.TotalSeconds * TrimRange.Minimum, _duration.TotalSeconds * TrimRange.Maximum);
            }
            else
            {
                // The whole video loops through Player_EndReached.
                _player.SetLoop(0, 0);
            }
        }

        private void TrimRange_MinimumChanged(object sender, double e)
        {
            if (_player == null)
            {
                return;
            }

            if (_player.IsPlaying)
            {
                _resume = true;
                _player.Pause();
            }

            UpdateLoop();
            _player.Position = _duration.TotalSeconds * e;
        }

        private void TrimRange_MaximumChanged(object sender, double e)
        {
            if (_player == null)
            {
                return;
            }

            UpdateLoop();
            _player.Position = _duration.TotalSeconds * e;

            // The cover's preview mirrors the player, so it stays on the cover until the
            // preview is gone; see ResumeAfterCover.
            if (_resume && !_coverDragging)
            {
                _resume = false;
                _player.Play();
            }
        }

        private void TrimRange_ViewChanged(object sender, EventArgs e)
        {
            if (TrimRange.ZoomLength < 1 && (TrimRange.ZoomStart != _zoomStart || TrimRange.ZoomLength != _zoomLength))
            {
                _zoomStart = TrimRange.ZoomStart;
                _zoomLength = TrimRange.ZoomLength;

                TrimZoomThumbnails.Children.Clear();
                TrimZoomThumbnails.Visibility = Visibility.Visible;

                InitializeZoomThumbnails(++_zoomVersion, _zoomStart, _zoomLength);
            }
            else if (TrimRange.ViewLength == 1 && _zoomLength < 1)
            {
                _zoomStart = 0;
                _zoomLength = 1;
                _zoomVersion++;

                TrimZoomThumbnails.Children.Clear();
                TrimZoomThumbnails.Visibility = Visibility.Collapsed;
            }

            // Both strips were laid out for a view of their own, the whole video and the zoomed
            // window, and move to where the track now draws that view. Until a zoomed frame is
            // decoded, the stretched strip behind it shows through.
            FollowTrim(TrimThumbnailsTransform, 0, 1);

            if (_zoomLength < 1)
            {
                FollowTrim(TrimZoomThumbnailsTransform, _zoomStart, _zoomLength);
            }

            if (_coverSurface != null)
            {
                UpdateDot();
                ApplyBubble();
            }
        }

        private void FollowTrim(CompositeTransform transform, double start, double length)
        {
            var from = TrimRange.GetPosition(0, start, length);
            var to = TrimRange.GetPosition(0, TrimRange.ViewStart, TrimRange.ViewLength);

            transform.ScaleX = (TrimRange.GetPosition(1, TrimRange.ViewStart, TrimRange.ViewLength) - to) / (TrimRange.GetPosition(1, start, length) - from);
            transform.TranslateX = to - from * transform.ScaleX;
        }

        #region Cover

        private void TrimRange_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_coverSurface != null)
            {
                UpdateDot();
            }
        }

        private void TrimRange_CoverChanged(object sender, EventArgs e)
        {
            if (_coverSurface == null)
            {
                return;
            }

            UpdateDot();

            if (_coverDragging)
            {
                ApplyBubble();
            }
        }

        private void TrimRange_HeadDraggingChanged(object sender, bool dragging)
        {
            if (_coverSurface == null)
            {
                return;
            }

            _coverDragging = dragging;

            _dot.Begin(dragging ? 1 : 0, DotDuration);
            StartRendering();

            if (dragging)
            {
                // The editor shows the frame while dragging, so the preview gets out of the way.
                if (_capture.IsRunning)
                {
                    _capture.Jump(0);
                    _bubble.Jump(0);
                    _bubbleVisible = false;
                }

                HideBubble();
                ApplyBubble();
            }
            else
            {
                StartCapture();
            }
        }

        // Again for every snapshot, as the frame may have moved or resized since.
        private bool StartMirror()
        {
            var frame = _coverSurface.GetFrameBounds(_coverSurface);
            if (frame.Width <= 0 || frame.Height <= 0)
            {
                return false;
            }

            var scale = BubbleSide / Math.Max(frame.Width, frame.Height);

            _bubbleSize = new Size(Math.Max(1, Math.Round(frame.Width * scale)), Math.Max(1, Math.Round(frame.Height * scale)));

            CoverBubble.Width = _bubbleSize.Width;
            CoverBubble.Height = _bubbleSize.Height;

            _mirrorShape.Size = new Vector2((float)_bubbleSize.Width, (float)_bubbleSize.Height);

            _mirror.SourceVisual = ElementCompositionPreview.GetElementVisual(_coverSurface);
            _mirror.SourceOffset = new Vector2((float)frame.X, (float)frame.Y);
            _mirror.SourceSize = new Vector2((float)frame.Width, (float)frame.Height);

            _mirroring = true;
            return true;
        }

        // The surface renders the frame a second time for as long as it is mirrored.
        private void StopMirror()
        {
            if (_mirroring)
            {
                _mirroring = false;
                _mirror.SourceVisual = null;
            }
        }

        // Paused for the drag and held on the cover while the preview showed it.
        private void ResumeAfterCover()
        {
            if (_resume && !_coverDragging && _player != null)
            {
                _resume = false;
                _player.Play();
            }
        }

        private void UpdateDot()
        {
            var cover = TrimRange.Cover;
            var start = TrimRange.ViewStart;
            var length = TrimRange.ViewLength;

            // Zoomed, the cover may be outside the window the track shows.
            CoverDot.Visibility = cover >= start && cover <= start + length
                ? Visibility.Visible
                : Visibility.Collapsed;

            var scale = (DotRestingSize + (DotSize - DotRestingSize) * _dot.Value) / DotSize;

            CoverDotTransform.TranslateX = TrimRange.GetPosition(cover, start, length) - DotSize / 2;
            CoverDotTransform.ScaleX = scale;
            CoverDotTransform.ScaleY = scale;
        }

        private void HideBubble()
        {
            _bubbleTimer?.Stop();

            if (_bubbleVisible)
            {
                _bubbleVisible = false;
                _bubble.Begin(0, BubbleDuration);
                StartRendering();
            }
        }

        private void StartCapture()
        {
            var from = StartMirror()
                ? _coverSurface.GetFrameBounds(CoverLayer)
                : Rect.Empty;

            if (from.IsEmpty || from.Width <= 0 || from.Height <= 0)
            {
                StopMirror();
                ResumeAfterCover();
                return;
            }

            _captureFrom = from;

            _bubbleTimer?.Stop();
            _bubbleVisible = false;
            _bubble.Jump(0);

            _capture.Jump(0);
            _capture.Begin(1, CaptureDuration);
            StartRendering();
        }

        // The frame is in the bubble, which now shows for a while.
        private void OnCaptured()
        {
            _bubbleVisible = true;
            _bubble.Jump(1);

            if (_bubbleTimer == null)
            {
                _bubbleTimer = new DispatcherTimer();
                _bubbleTimer.Interval = TimeSpan.FromMilliseconds(BubbleVisible);
                _bubbleTimer.Tick += BubbleTimer_Tick;
            }

            _bubbleTimer.Start();
        }

        private void BubbleTimer_Tick(object sender, object e)
        {
            HideBubble();
        }

        // Positioned by its transform alone, so that following the playhead or animating lays
        // nothing out. Its border and corners are divided by its scale to stay as they are.
        private void ApplyBubble()
        {
            var capturing = _capture.IsRunning;
            var shown = _bubble.Value;

            if (!_mirroring || !capturing && shown <= 0)
            {
                CoverBubble.Visibility = Visibility.Collapsed;

                StopMirror();
                ResumeAfterCover();
                return;
            }

            var width = _bubbleSize.Width;
            var height = _bubbleSize.Height;

            var dotX = TrimRange.GetPosition(TrimRange.Cover, TrimRange.ViewStart, TrimRange.ViewLength);
            var left = dotX - width / 2;
            var top = -DotRise - DotSize / 2 - BubbleSkip - height;

            double scale;

            if (capturing)
            {
                var value = _capture.Value;

                var x = _captureFrom.X + (left - _captureFrom.X) * value;
                var y = _captureFrom.Y + (top - _captureFrom.Y) * value;
                var w = _captureFrom.Width + (width - _captureFrom.Width) * value;
                var h = _captureFrom.Height + (height - _captureFrom.Height) * value;

                scale = w / width;

                CoverBubbleTransform.ScaleX = scale;
                CoverBubbleTransform.ScaleY = h / height;
                CoverBubbleTransform.TranslateX = x;
                CoverBubbleTransform.TranslateY = y;

                CoverBubble.Opacity = 1;
                CoverBubbleStroke.Opacity = value * value;
            }
            else
            {
                // Shrinks into the dot, its centre falling from where it rests to the dot.
                var dotY = -DotRise;
                var centerY = dotY + (top + height / 2 - dotY) * shown;

                scale = shown;

                CoverBubbleTransform.ScaleX = scale;
                CoverBubbleTransform.ScaleY = scale;
                CoverBubbleTransform.TranslateX = dotX - width * shown / 2;
                CoverBubbleTransform.TranslateY = centerY - height * shown / 2;

                CoverBubble.Opacity = shown;
                CoverBubbleStroke.Opacity = 1;
            }

            scale = Math.Max(scale, 0.01);

            CoverBubbleFrame.BorderThickness = new Thickness(BubbleBorder / scale);
            CoverBubbleFrame.CornerRadius = new CornerRadius(BubbleRadius / scale);
            CoverBubble.Visibility = Visibility.Visible;

            _mirrorShape.CornerRadius = new Vector2((float)(BubbleRadius / scale));
        }

        private void StartRendering()
        {
            if (!_rendering)
            {
                _rendering = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            var time = ((RenderingEventArgs)e).RenderingTime;
            var capturing = _capture.IsRunning;

            _dot.Step(time);
            _bubble.Step(time);
            _capture.Step(time);

            if (capturing && !_capture.IsRunning)
            {
                OnCaptured();
            }

            UpdateDot();
            ApplyBubble();

            if (!_dot.IsRunning && !_bubble.IsRunning && !_capture.IsRunning)
            {
                StopRendering();
            }
        }

        // A value eased out (quintic) from where it is to a target, stepped by the frame clock.
        private sealed class Tween
        {
            private double _from;
            private double _to;
            private double _duration;
            private TimeSpan? _start;

            public double Value { get; private set; }

            public bool IsRunning { get; private set; }

            public void Begin(double to, double duration)
            {
                _from = Value;
                _to = to;
                _duration = duration;
                _start = null;

                IsRunning = true;
            }

            public void Jump(double value)
            {
                Value = value;
                IsRunning = false;
            }

            public void Step(TimeSpan time)
            {
                if (!IsRunning)
                {
                    return;
                }

                _start ??= time;

                var t = Math.Min(1, (time - _start.Value).TotalMilliseconds / _duration);
                var eased = 1 - Math.Pow(1 - t, 5);

                Value = _from + (_to - _from) * eased;

                if (t >= 1)
                {
                    Value = _to;
                    IsRunning = false;
                }
            }
        }

        #endregion
    }
}
