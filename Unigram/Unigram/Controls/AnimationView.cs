using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Buffers;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Native;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class AnimationView30Fps : AnimationView
    {
        public AnimationView30Fps()
            : base(true)
        {

        }
    }

    [TemplatePart(Name = "Thumbnail", Type = typeof(ImageBrush))]
    public class AnimationView : AnimatedControl<IVideoAnimationSource, CachedVideoAnimation>, IPlayerView
    {
        private ImageBrush _thumbnail;
        private bool? _hideThumbnail;

        private int _prevSeconds = int.MaxValue;
        private int _nextSeconds;

        private bool _isCachingEnabled;

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
            _thumbnail = GetTemplateChild("Thumbnail") as ImageBrush;

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

        protected override CanvasBitmap CreateBitmap(ICanvasResourceCreator sender)
        {
            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != _animation?.PixelWidth || _bitmap?.Size.Height != _animation?.PixelHeight;

            if (needsCreate && _animation != null)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(_animation.PixelWidth * _animation.PixelHeight * 4);
                var bitmap = CanvasBitmap.CreateFromBytes(sender, buffer, _animation.PixelWidth, _animation.PixelHeight, DirectXPixelFormat.R8G8B8A8UIntNormalized);
                ArrayPool<byte>.Shared.Return(buffer);

                return bitmap;
            }

            return needsCreate ? null : _bitmap;
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            var width = (double)_animation.PixelWidth;
            var height = (double)_animation.PixelHeight;

            if (_stretch == Stretch.UniformToFill)
            {
                double ratioX = (double)sender.Size.Width / width;
                double ratioY = (double)sender.Size.Height / height;

                if (ratioX > ratioY)
                {
                    width = sender.Size.Width;
                    height *= ratioX;
                }
                else
                {
                    width *= ratioY;
                    height = sender.Size.Height;
                }
            }
            else if (_stretch == Stretch.Uniform)
            {
                double ratioX = (double)sender.Size.Width / width;
                double ratioY = (double)sender.Size.Height / height;

                if (ratioX <= ratioY)
                {
                    width = sender.Size.Width;
                    height *= ratioX;
                }
                else
                {
                    width *= ratioY;
                    height = sender.Size.Height;
                }
            }

            var y = (sender.Size.Height - height) / 2;
            var x = (sender.Size.Width - width) / 2;

            if (width >= _bitmap.Size.Width || height >= _bitmap.Size.Height)
            {
                args.DrawImage(_bitmap,
                    new Rect(x, y, width, height));
            }
            else
            {
                args.DrawImage(_bitmap,
                    new Rect(x, y, width, height),
                    new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height), 1,
                    CanvasImageInterpolation.MultiSampleLinear);
            }

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

        protected override void NextFrame()
        {
            var animation = _animation;
            if (animation == null || animation.IsCaching || _surface == null || _bitmap == null || _unloaded)
            {
                return;
            }

            animation.RenderSync(_bitmap, out _nextSeconds);

            if (_hideThumbnail == null)
            {
                _hideThumbnail = true;
            }
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

            var shouldPlay = _shouldPlay;

            var animation = await Task.Run(() => CachedVideoAnimation.LoadFromFile(newValue, _isCachingEnabled));
            if (animation == null || newValue?.Id != _source?.Id)
            {
                // The app can't access the file specified
                Client.Execute(new AddLogMessage(5, $"Can't load animation for playback: {newValue.FilePath}"));
                return;
            }

            if (_shouldPlay)
            {
                shouldPlay = true;
            }

            _interval = TimeSpan.FromMilliseconds(1000d / Math.Min(30, animation.FrameRate));
            _animation = animation;
            _hideThumbnail = null;

            OnSourceChanged();
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
