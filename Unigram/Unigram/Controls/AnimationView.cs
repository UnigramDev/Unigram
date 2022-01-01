using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Native;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    [TemplatePart(Name = "Thumbnail", Type = typeof(Image))]
    public class AnimationView : Control, IPlayerView
    {
        private CanvasControl _canvas;
        private CanvasBitmap _bitmap;

        private Grid _layoutRoot;

        private Image _thumbnail;
        private bool? _hideThumbnail;

        private IVideoAnimationSource _source;
        private VideoAnimation _animation;

        private bool _shouldPlay;

        private bool _isLoopingEnabled = true;

        //private readonly LoopThread _thread = LoopThreadPool.Animations.Get();
        private LoopThread _thread;
        private bool _subscribed;

        private bool _unloaded;

        private int _prevSeconds = int.MaxValue;
        private int _nextSeconds;

        public AnimationView()
        {
            DefaultStyleKey = typeof(AnimationView);
        }

        public bool LimitFps { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild("Canvas") as CanvasControl;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;
            _canvas.Draw += OnDraw;

            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _layoutRoot.Loaded += OnLoaded;
            _layoutRoot.Loading += OnLoading;
            _layoutRoot.Unloaded += OnUnloaded;

            _thumbnail = (Image)GetTemplateChild("Thumbnail");

            OnSourceChanged(Source, _source);

            base.OnApplyTemplate();
        }

        private bool Load()
        {
            if (_unloaded && _layoutRoot != null && _layoutRoot.IsLoaded)
            {
                while (_layoutRoot.Children.Count > 1)
                {
                    _layoutRoot.Children.Remove(_layoutRoot.Children[1]);
                }

                _canvas = new CanvasControl();
                _canvas.Draw += OnDraw;

                _layoutRoot.Children.Add(_canvas);

                _unloaded = false;
                OnSourceChanged(Source, _source);

                return true;
            }

            return false;
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            Load();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        public void Unload()
        {
            _shouldPlay = false;
            _unloaded = true;
            Subscribe(false);

            if (_canvas != null)
            {
                _canvas.Draw -= OnDraw;
                _canvas.RemoveFromVisualTree();
                _canvas = null;
            }

            _source = null;

            //_bitmap?.Dispose();
            _bitmap = null;

            //_animation?.Dispose();
            _animation = null;

            _thread = null;
        }

        private void OnTick(object sender, EventArgs args)
        {
            try
            {
                Invalidate();
            }
            catch
            {
                _ = Dispatcher.RunIdleAsync(idle => Subscribe(false));
            }
        }

        private void OnInvalidate(object sender, EventArgs e)
        {
            _canvas?.Invalidate();
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_animation == null || _unloaded)
            {
                return;
            }

            if (_bitmap == null)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(_animation.PixelWidth * _animation.PixelHeight * 4);
                _bitmap = CanvasBitmap.CreateFromBytes(_canvas, buffer, _animation.PixelWidth, _animation.PixelHeight, DirectXPixelFormat.R8G8B8A8UIntNormalized);
                ArrayPool<byte>.Shared.Return(buffer);

                return;
            }

            var width = (double)_animation.PixelWidth;
            var height = (double)_animation.PixelHeight;
            var x = 0d;
            var y = 0d;

            //if (width > sender.Size.Width || height > sender.Size.Height)
            {
                double ratioX = (double)sender.Size.Width / width;
                double ratioY = (double)sender.Size.Height / height;

                if (ratioX > ratioY)
                {
                    width = sender.Size.Width;
                    height *= ratioX;
                    y = (sender.Size.Height - height) / 2;
                }
                else
                {
                    width *= ratioY;
                    height = sender.Size.Height;
                    x = (sender.Size.Width - width) / 2;
                }
            }

            args.DrawingSession.DrawImage(_bitmap, new Rect(x, y, width, height));

            if (_prevSeconds != _nextSeconds)
            {
                _prevSeconds = _nextSeconds;
                PositionChanged?.Invoke(this, _nextSeconds);
            }

            if (_hideThumbnail == true && _thumbnail != null)
            {
                _hideThumbnail = false;
                _thumbnail.Opacity = 0;
            }
        }

        public void Invalidate()
        {
            var animation = _animation;
            if (animation == null || _canvas == null || _bitmap == null || _unloaded)
            {
                return;
            }

            //_bitmap = animation.RenderSync(_device, index, 256, 256);
            animation.RenderSync(_bitmap, false, out _nextSeconds);

            if (_hideThumbnail == null)
            {
                _hideThumbnail = true;
            }
        }

        private async void OnSourceChanged(IVideoAnimationSource newValue, IVideoAnimationSource oldValue)
        {
            var canvas = _canvas;
            if (canvas == null && !Load())
            {
                return;
            }

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

            var animation = await Task.Run(() => VideoAnimation.LoadFromFile(newValue, false, LimitFps));
            if (animation == null || newValue?.Id != _source?.Id)
            {
                // The app can't access the file specified
                return;
            }

            if (_shouldPlay)
            {
                shouldPlay = true;
            }

            _animation = animation;
            _bitmap = null;

            if (AutoPlay || _shouldPlay)
            {
                _shouldPlay = false;
                Subscribe(true);
            }
            else if (!_unloaded)
            {
                Subscribe(false);

                if (_canvas.ReadyToDraw)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(_animation.PixelWidth * _animation.PixelHeight * 4);
                    _bitmap = CanvasBitmap.CreateFromBytes(_canvas, buffer, _animation.PixelWidth, _animation.PixelHeight, DirectXPixelFormat.R8G8B8A8UIntNormalized);
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // Invalidate to render the first frame
                await Task.Run(Invalidate);
                _canvas?.Invalidate();
            }
        }

        public bool Play()
        {
            Load();

            var canvas = _canvas;
            if (canvas == null)
            {
                _shouldPlay = true;
                return false;
            }

            var animation = _animation;
            if (animation == null)
            {
                _shouldPlay = true;
                return false;
            }

            _shouldPlay = false;

            if (_subscribed)
            {
                return false;
            }

            //canvas.Paused = false;
            Subscribe(true);
            return true;
            //OnInvalidate();
        }

        public void Pause()
        {
            var canvas = _canvas;
            if (canvas == null)
            {
                //_source = newValue;
                return;
            }

            //canvas.Paused = true;
            //canvas.ResetElapsedTime();
            Subscribe(false);
        }

        private void Subscribe(bool subscribe)
        {
            _subscribed = subscribe;

            if (_thread == null && !LimitFps)
            {
                if (_animation != null)
                {
                    _thread = LoopThreadPool.Get(_animation.FrameRate);
                }
                else
                {
                    return;
                }
            }
            else
            {
                _thread ??= LoopThreadPool.Animations.Get();
            }

            _thread.Tick -= OnTick;
            LoopThread.Animations.Invalidate -= OnInvalidate;

            if (subscribe)
            {
                _thread.Tick += OnTick;
                LoopThread.Animations.Invalidate += OnInvalidate;
            }
        }

        private string UriToPath(Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            switch (uri.Scheme)
            {
                case "ms-appx":
                    return Path.Combine(uri.Segments.Select(x => x.Trim('/')).ToArray());
                case "ms-appdata":
                    switch (uri.Host)
                    {
                        case "local":
                            return Path.Combine(new[] { ApplicationData.Current.LocalFolder.Path }.Union(uri.Segments.Select(x => x.Trim('/'))).ToArray());
                        case "temp":
                            return Path.Combine(new[] { ApplicationData.Current.TemporaryFolder.Path }.Union(uri.Segments.Select(x => x.Trim('/'))).ToArray());
                    }
                    break;
                case "file":
                    return uri.LocalPath;
            }

            return null;
        }

        public event EventHandler<int> PositionChanged;

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get => (bool)GetValue(IsLoopingEnabledProperty);
            set => SetValue(IsLoopingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(AnimationView), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimationView)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimationView), new PropertyMetadata(true));

        #endregion

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

    }
}
