using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Unigram.Common;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasAnimatedControl))]
    [TemplatePart(Name = "Thumbnail", Type = typeof(Image))]
    public class LottieView : Control
    {
        private CanvasSwapChainPanel _canvas;
        private string CanvasPartName = "Canvas";

        private CanvasSwapChain _swapChain;

        private Image _thumbnail;

        private string _source;
        private CachedAnimation _animation;

        private bool _animationIsCached;
        private bool _animationIsCaching;

        private double _animationFrameRate;
        private int _animationTotalFrame;

        private bool _shouldPlay;

        // Detect from hardware?
        private bool _limitFps = true;

        private int _index;
        private bool _backward;

        private bool _isLoopingEnabled = true;
        private bool _isCachingEnabled = true;

        private bool _hideThumbnail = true;

        private LottieLoopThread _thread = LottieLoopThread.Current;
        private bool _subscribed;

        private CanvasDevice _device;

        private Vector2 _logicalSize;
        private float _logicalDpi;

        public LottieView()
        {
            DefaultStyleKey = typeof(LottieView);
        }

        //~LottieView()
        //{
        //    Dispose();
        //}

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild(CanvasPartName) as CanvasSwapChainPanel;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;
            _canvas.Unloaded += OnUnloaded;
            //_canvas.SwapChain = _swapChain;
            //_canvas.CreateResources += OnCreateResources;
            //_canvas.RegionsInvalidated += OnRegionsInvalidated;

            _thumbnail = (Image)GetTemplateChild("Thumbnail");

            OnSourceChanged(UriToPath(Source), _source);

            base.OnApplyTemplate();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _logicalDpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            _logicalSize = finalSize.ToVector2();

            _swapChain?.ResizeBuffers(_logicalSize.X, _logicalSize.Y, _logicalDpi);

            Invalidate();

            return base.ArrangeOverride(finalSize);
        }

        public void Dispose()
        {
            //if (_animation is IDisposable disposable)
            //{
            //    Debug.WriteLine("Disposing animation for: " + Path.GetFileName(_source));
            //    disposable.Dispose();
            //}

            //_animation = null;
            _source = null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Subscribe(false);

            _canvas.Unloaded -= OnUnloaded;
            _canvas.RemoveFromVisualTree();
            _canvas = null;

            Dispose();
        }

        private void OnTick(object sender, EventArgs args)
        {
            try
            {
                Invalidate();
            }
            catch
            {
                Subscribe(false);
            }
        }

        public void Invalidate()
        {
            var animation = _animation;
            if (animation == null || _animationIsCaching || _canvas == null || _logicalSize == Vector2.Zero)
            {
                return;
            }

            if (_swapChain == null)
            {
                if (_thread.Cooldown > 0 && _subscribed)
                {
                    return;
                }

                _thread.Cooldown = 2;
                _device = new CanvasDevice();

                _swapChain = new CanvasSwapChain(_device, _logicalSize.X, _logicalSize.Y, _logicalDpi);

                _ = Dispatcher.RunIdleAsync(idle =>
                {
                    var canvas = _canvas;
                    if (canvas != null)
                    {
                        canvas.SwapChain = _swapChain;
                    }
                });
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            if (!_animationIsCached)
            {
                Debug.WriteLine("Rendering first frame for: " + Path.GetFileName(_source));
            }

            using (var session = _swapChain.CreateDrawingSession(Colors.Transparent))
            using (var bitmap = animation.RenderSync(session, index, 256, 256))
            {
                session.DrawImage(bitmap, new Rect(0, 0, _swapChain.Size.Width, _swapChain.Size.Height));
            }

            _swapChain.Present();

            if (!_animationIsCached)
            {
                _animationIsCached = true;
                _animationIsCaching = true;

                Debug.WriteLine("Creating cache for: " + Path.GetFileName(_source));

                ThreadPool.QueueUserWorkItem(state =>
                {
                    if (animation is CachedAnimation cached)
                    {
                        cached.CreateCache(256, 256);
                        _animationIsCaching = false;
                    }
                }, animation);
            }

            if (_hideThumbnail && _thumbnail != null)
            {
                _hideThumbnail = false;
                this.BeginOnUIThread(() =>
                {
                    _thumbnail.Opacity = 0;
                });
            }

            IndexChanged?.Invoke(this, index);

            if (_backward)
            {
                if (index - framesPerUpdate > 0)
                {
                    _index -= framesPerUpdate;
                }
                else
                {
                    _index = 0;
                    _backward = false;

                    if (!_isLoopingEnabled)
                    {
                        //sender.Paused = true;
                        //sender.ResetElapsedTime();
                        Subscribe(false);
                    }
                }

                return;
            }

            if (index + framesPerUpdate < _animationTotalFrame)
            {
                PositionChanged?.Invoke(this, Math.Min(1, Math.Max(0, (double)(index + 1) / _animationTotalFrame)));

                _index += framesPerUpdate;
            }
            else
            {
                _index = 0;

                if (!_isLoopingEnabled)
                {
                    //sender.Paused = true;
                    //sender.ResetElapsedTime();
                    Subscribe(false);
                }

                PositionChanged?.Invoke(this, 1);
            }
        }

        public void SetPosition(double position)
        {
            if (position < 0 || position > 1)
            {
                return;
            }

            var animation = _animation;
            if (animation == null)
            {
                return;
            }

            _index = (int)Math.Min(_animation.TotalFrame - 1, Math.Ceiling(_animation.TotalFrame * position));
        }

        public int Ciccio => _animation.TotalFrame;
        public int Index => _index == int.MaxValue ? 0 : _index;

        private void OnSourceChanged(Uri newValue, Uri oldValue)
        {
            OnSourceChanged(UriToPath(newValue), UriToPath(oldValue));
        }

        private void OnSourceChanged(string newValue, string oldValue)
        {
            var canvas = _canvas;
            if (canvas == null)
            {
                return;
            }

            if (newValue == null)
            {
                //canvas.Paused = true;
                //canvas.ResetElapsedTime();
                Subscribe(false);

                Dispose();
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var animation = CachedAnimation.LoadFromFile(newValue, _isCachingEnabled, _limitFps, ColorReplacements);
            if (animation == null)
            {
                // The app can't access the file specified
                return;
            }

            _source = newValue;
            _animation = animation;

            _animationIsCached = animation.IsCached;
            _animationFrameRate = animation.FrameRate;
            _animationTotalFrame = animation.TotalFrame;

            _index = _isCachingEnabled ? 0 : _animationTotalFrame - 1;

            var update = TimeSpan.FromSeconds(_animation.Duration / _animation.TotalFrame);
            if (_limitFps && _animation.FrameRate >= 60)
            {
                update = TimeSpan.FromSeconds(update.TotalSeconds * 2);
            }

            //canvas.Paused = true;
            //canvas.ResetElapsedTime();
            //canvas.TargetElapsedTime = update > TimeSpan.Zero ? update : TimeSpan.MaxValue;

            if (AutoPlay || _shouldPlay)
            {
                _shouldPlay = false;
                Subscribe(true);
                //canvas.Paused = false;
            }
            else
            {
                Subscribe(false);

                // Invalidate to render the first frame
                Invalidate();
            }
        }

        public void Play(bool backward = false)
        {
            var canvas = _canvas;
            if (canvas == null)
            {
                _shouldPlay = true;
                return;
            }

            var animation = _animation;
            if (animation == null)
            {
                _shouldPlay = true;
                return;
            }

            _shouldPlay = false;
            _backward = backward;

            //canvas.Paused = false;
            Subscribe(true);
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
            _thread.Tick -= OnTick;

            if (subscribe)
            {
                _thread.Tick += OnTick;
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

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get { return (bool)GetValue(IsLoopingEnabledProperty); }
            set { SetValue(IsLoopingEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(LottieView), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region IsCachingEnabled

        public bool IsCachingEnabled
        {
            get { return (bool)GetValue(IsCachingEnabledProperty); }
            set { SetValue(IsCachingEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsCachingEnabledProperty =
            DependencyProperty.Register("IsCachingEnabled", typeof(bool), typeof(LottieView), new PropertyMetadata(true, OnCachingEnabledChanged));

        private static void OnCachingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._isCachingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(LottieView), new PropertyMetadata(true));

        #endregion

        #region Source

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(LottieView), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d).OnSourceChanged((Uri)e.NewValue, (Uri)e.OldValue);
        }

        #endregion

        #region Thumbnail

        public ImageSource Thumbnail
        {
            get { return (ImageSource)GetValue(ThumbnailProperty); }
            set { SetValue(ThumbnailProperty, value); }
        }

        public static readonly DependencyProperty ThumbnailProperty =
            DependencyProperty.Register("Thumbnail", typeof(ImageSource), typeof(LottieView), new PropertyMetadata(null));

        #endregion

        public event EventHandler<double> PositionChanged;
        public event EventHandler<int> IndexChanged;

        public IReadOnlyDictionary<uint, uint> ColorReplacements { get; set; }
    }

    public class LottieLoopThread
    {
        private readonly Timer _timer;

        private bool _drop;
        public int Cooldown;

        public LottieLoopThread()
        {
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        private static LottieLoopThread _current;
        public static LottieLoopThread Current => _current ??= new LottieLoopThread();

        private void OnTick(object state)
        {
            if (_drop)
            {
                return;
            }

            _drop = true;
            _tick?.Invoke(this, EventArgs.Empty);
            _drop = false;

            if (Cooldown > 0)
            {
                Cooldown--;
            }
        }

        private event EventHandler _tick;
        public event EventHandler Tick
        {
            add
            {
                if (_tick == null) _timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 30));
                _tick += value;
            }
            remove
            {
                _tick -= value;
                if (_tick == null) _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }
}
