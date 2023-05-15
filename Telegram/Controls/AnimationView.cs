//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public class AnimationView30Fps : AnimationView
    {
        public AnimationView30Fps()
            : base(true)
        {

        }
    }

    [TemplatePart(Name = "Thumbnail", Type = typeof(ImageBrush))]
    public class AnimationView : AnimatedImage<CachedVideoAnimation>
    {
        private Image _thumbnail;
        private bool? _hideThumbnail;

        private int _prevSeconds = int.MaxValue;
        private int _nextSeconds;

        private bool _isCachingEnabled;
        private bool _shouldStopNextFrame;

        private IVideoAnimationSource _source;

        public AnimationView()
            : this(null)
        {
        }

        protected AnimationView(bool? limitFps)
            : base(limitFps)
        {
            DefaultStyleKey = typeof(AnimationView);
        }

        ~AnimationView()
        {
            System.Diagnostics.Debug.WriteLine("~AnimationView");
        }

        protected override void OnApplyTemplate()
        {
            _thumbnail = GetTemplateChild("Thumbnail") as Image;

            base.OnApplyTemplate();
        }

        protected override void SourceChanged()
        {
            if (Source != null)
            {
                OnSourceChanged(Source, _source);
            }
        }

        protected override void Dispose()
        {
            if (_animation != null && !_animation.IsCaching)
            {
                _animation.Dispose();
            }

            _source = null;
            _animation = null;
        }

        protected override bool CreateBitmap(double rasterizationScale, out int width, out int height)
        {
            if (_animation != null)
            {
                width = _animation.PixelWidth;
                height = _animation.PixelHeight;
                return true;
            }

            width = 0;
            height = 0;
            return false;
        }

        protected override void DrawFrame(WriteableBitmap bitmap)
        {
            if (_prevSeconds != _nextSeconds)
            {
                _prevSeconds = _nextSeconds;
                PositionChanged?.Invoke(this, _nextSeconds);
            }

            if (_hideThumbnail == true && _thumbnail != null)
            {
                _hideThumbnail = false;
                _thumbnail.Opacity = 0;

                FirstFrameRendered?.Invoke(this, EventArgs.Empty);
                ElementCompositionPreview.SetElementChildVisual(this, null);
            }
        }

        protected override bool NextFrame(PixelBuffer pixels)
        {
            var animation = _animation;
            if (animation == null || animation.IsCaching || _unloaded)
            {
                return false;
            }

            // This line should not be needed, but we keep it anyway for now
            if (animation.PixelWidth != pixels.PixelWidth || animation.PixelHeight != pixels.PixelHeight)
            {
                Logger.Debug();
                return false;
            }

            if (_shouldStopNextFrame)
            {
                _shouldStopNextFrame = false;
                animation.Stop();
            }

            animation.RenderSync(pixels, pixels.PixelWidth, pixels.PixelHeight, out _nextSeconds, out bool completed);

            if (completed && !_isLoopingEnabled)
            {
                Pause();
            }

            _hideThumbnail ??= true;
            return true;
        }

        private async void OnSourceChanged(IVideoAnimationSource newValue, IVideoAnimationSource oldValue)
        {
            //var canvas = _canvas;
            //if (canvas == null && !Load())
            //{
            //    return;
            //}

            if (newValue == null)
            {
                Unload();
                return;
            }

            if (newValue?.Id == oldValue?.Id || newValue?.Id == _source?.Id)
            {
                return;
            }

            _source = newValue;

            var animation = await Task.Run(() => CachedVideoAnimation.LoadFromFile(newValue, 0, 0, _isCachingEnabled));
            if (animation == null || newValue?.Id != _source?.Id || double.IsNaN(animation.FrameRate))
            {
                // The app can't access the file specified
                return;
            }

            var frameRate = 1000d / Math.Clamp(animation.FrameRate, 1, _isCachingEnabled ? 30 : 60);

            await _nextFrameLock.WaitAsync();

            _needToCreateBitmap = true;

            _interval = TimeSpan.FromMilliseconds(frameRate);
            _animation = animation;
            _hideThumbnail = null;

            //CreateBitmap();

            lock (_drawFrameLock)
            {
                CreateBitmap();
            }

            _nextFrameLock.Release();

            if (Load())
            {
                return;
            }

            OnSourceChanged();
        }

        public void Stop()
        {
            _shouldStopNextFrame = true;
            Pause();
        }

        public event EventHandler<int> PositionChanged;

        public event EventHandler FirstFrameRendered;

        #region Source

        public IVideoAnimationSource Source
        {
            get => (IVideoAnimationSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(IVideoAnimationSource), typeof(AnimationView), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimationView)d).OnSourceChanged((IVideoAnimationSource)e.NewValue, (IVideoAnimationSource)e.OldValue);
        }

        #endregion

        #region Thumbnail

        public ImageSource Thumbnail
        {
            get => (ImageSource)GetValue(ThumbnailProperty);
            set => SetValue(ThumbnailProperty, value);
        }

        public static readonly DependencyProperty ThumbnailProperty =
            DependencyProperty.Register("Thumbnail", typeof(ImageSource), typeof(AnimationView), new PropertyMetadata(null));

        #endregion

        #region IsCachingEnabled

        public bool IsCachingEnabled
        {
            get => (bool)GetValue(IsCachingEnabledProperty);
            set => SetValue(IsCachingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsCachingEnabledProperty =
            DependencyProperty.Register("IsCachingEnabled", typeof(bool), typeof(AnimationView), new PropertyMetadata(false, OnCachingEnabledChanged));

        private static void OnCachingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimationView)d)._isCachingEnabled = (bool)e.NewValue;
        }

        #endregion
    }
}
