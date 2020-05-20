using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    [TemplatePart(Name = "Thumbnail", Type = typeof(Image))]
    public class LottieView : Control
    {
        private CanvasControl _canvas;
        private string CanvasPartName = "Canvas";

        private CanvasBitmap _bitmap;

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

        private ICanvasResourceCreator _device;

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
            var canvas = GetTemplateChild(CanvasPartName) as CanvasControl;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;
            _canvas.CreateResources += OnCreateResources;
            _canvas.Draw += OnDraw;
            _canvas.Unloaded += OnUnloaded;

            //_canvas.SwapChain = _swapChain;
            //_canvas.CreateResources += OnCreateResources;
            //_canvas.RegionsInvalidated += OnRegionsInvalidated;

            _thumbnail = (Image)GetTemplateChild("Thumbnail");

            OnSourceChanged(UriToPath(Source), _source);

            base.OnApplyTemplate();
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

            _canvas.CreateResources -= OnCreateResources;
            _canvas.Draw -= OnDraw;
            _canvas.Unloaded -= OnUnloaded;
            _canvas.RemoveFromVisualTree();
            _canvas = null;

            Dispose();

            _animation?.Dispose();
            _bitmap?.Dispose();
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

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            _device = sender;

            if (args.Reason == CanvasCreateResourcesReason.FirstTime)
            {
                OnSourceChanged(UriToPath(Source), _source);
                Invalidate();
            }
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _device = args.DrawingSession.Device;

            if (_bitmap != null)
            {
                args.DrawingSession.DrawImage(_bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));

                if (_hideThumbnail && _thumbnail != null)
                {
                    _hideThumbnail = false;
                    _thumbnail.Opacity = 0;
                }
            }
        }

        public void Invalidate()
        {
            var animation = _animation;
            if (animation == null || _animationIsCaching || _canvas == null || _device == null)
            {
                return;
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            _bitmap = animation.RenderSync(_device, index, 256, 256);

            if (!_animationIsCached)
            {
                _animationIsCached = true;
                _animationIsCaching = true;

                ThreadPool.QueueUserWorkItem(state =>
                {
                    if (animation is CachedAnimation cached)
                    {
                        cached.CreateCache(256, 256);
                        _animationIsCaching = false;
                    }
                }, animation);
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
                        _ = Dispatcher.RunIdleAsync(idle => Subscribe(false));
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
                    _ = Dispatcher.RunIdleAsync(idle => Subscribe(false));
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
                _canvas.Invalidate();
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
            _thread.Invalidate -= OnInvalidate;

            if (subscribe)
            {
                _thread.Tick += OnTick;
                _thread.Invalidate += OnInvalidate;
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
        private readonly Timer _timerTick;
        private readonly DispatcherTimer _timerInvalidate;

        private bool _dropTick;
        private bool _dropInvalidate;

        public LottieLoopThread()
        {
            _timerTick = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);

            _timerInvalidate = new DispatcherTimer();
            _timerInvalidate.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            _timerInvalidate.Tick += OnInvalidate;
        }

        private static LottieLoopThread _current;
        public static LottieLoopThread Current => _current ??= new LottieLoopThread();

        private void OnTick(object state)
        {
            if (_dropTick)
            {
                return;
            }

            _dropTick = true;
            _tick?.Invoke(this, EventArgs.Empty);
            _dropTick = false;
        }

        private void OnInvalidate(object sender, object e)
        {
            if (_dropInvalidate)
            {
                return;
            }

            _dropInvalidate = true;
            _invalidate?.Invoke(this, EventArgs.Empty);
            _dropInvalidate = false;
        }

        private event EventHandler _tick;
        public event EventHandler Tick
        {
            add
            {
                if (_tick == null) _timerTick.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 30));
                _tick += value;
            }
            remove
            {
                _tick -= value;
                if (_tick == null) _timerTick.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private event EventHandler _invalidate;
        public event EventHandler Invalidate
        {
            add
            {
                if (_invalidate == null) _timerInvalidate.Start();
                _invalidate += value;
            }
            remove
            {
                _invalidate -= value;
                if (_invalidate == null) _timerInvalidate.Stop();
            }
        }
    }
}
