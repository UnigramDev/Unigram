//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
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

        public TrimToolbar()
        {
            InitializeComponent();
        }

        // Raised when the user toggles it, not when IsMuted is set.
        public event EventHandler MuteChanged;

        public bool CanMute
        {
            get => MuteButton.Visibility == Visibility.Visible;
            set => MuteButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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

            if (_resume)
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
        }

        private void FollowTrim(CompositeTransform transform, double start, double length)
        {
            var from = TrimRange.GetPosition(0, start, length);
            var to = TrimRange.GetPosition(0, TrimRange.ViewStart, TrimRange.ViewLength);

            transform.ScaleX = (TrimRange.GetPosition(1, TrimRange.ViewStart, TrimRange.ViewLength) - to) / (TrimRange.GetPosition(1, start, length) - from);
            transform.TranslateX = to - from * transform.ScaleX;
        }
    }
}
