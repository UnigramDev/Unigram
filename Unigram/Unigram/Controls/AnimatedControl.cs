using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Graphics.Display;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(Image))]
    public abstract class AnimatedControl<TSource, TAnimation> : Control, IPlayerView
    {
        private Vector2 _currentSize;
        private float _currentDpi;
        private bool _visible = true;

        protected CanvasImageSource _surface;
        protected CanvasBitmap _bitmap;

        private Grid _layoutRoot;
        protected Image _canvas;

        protected TSource _source;
        protected TAnimation _animation;

        protected bool _shouldPlay;

        protected bool _subscribed;
        protected bool _unsubscribe;

        protected bool _unloaded;

        protected bool _isLoopingEnabled = true;
        protected Stretch _stretch = Stretch.Uniform;

        private readonly object _recreateLock = new();
        private readonly object _drawFrameLock = new();
        private readonly SemaphoreSlim _nextFrameLock = new(1, 1);

        private ThreadPoolTimer _timer;

        protected TimeSpan _interval;
        private TimeSpan _elapsed;

        // Better hardware detection?
        protected readonly bool _limitFps = true;

        protected AnimatedControl(bool? limitFps)
        {
            _limitFps = limitFps ?? !Windows.UI.Composition.CompositionCapabilities.GetForCurrentView().AreEffectsFast();
            _currentDpi = DisplayInformation.GetForCurrentView().LogicalDpi;

            SizeChanged += OnSizeChanged;
        }

        ~AnimatedControl()
        {
            System.Diagnostics.Debug.WriteLine("~AnimatedControl");
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild("Canvas") as Image;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;

            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _layoutRoot.Loaded += OnLoaded;
            _layoutRoot.Loading += OnLoading;
            _layoutRoot.Unloaded += OnUnloaded;

            SourceChanged();

            base.OnApplyTemplate();
        }

        private void RegisterEventHandlers()
        {
            DisplayInformation.GetForCurrentView().DpiChanged += OnDpiChanged;
            Window.Current.Activated += OnActivated;
            CompositionTarget.SurfaceContentsLost += OnSurfaceContentsLost;
        }

        private void UnregisterEventHandlers()
        {
            DisplayInformation.GetForCurrentView().DpiChanged -= OnDpiChanged;
            Window.Current.Activated -= OnActivated;
            CompositionTarget.SurfaceContentsLost -= OnSurfaceContentsLost;
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            lock (_drawFrameLock)
            {
                _currentDpi = sender.LogicalDpi;

                Changed();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (_drawFrameLock)
            {
                _currentSize = e.NewSize.ToVector2();

                Changed();
            }
        }

        private void OnSurfaceContentsLost(object sender, object e)
        {
            lock (_drawFrameLock)
            {
                _surface = null;
                _bitmap = null;

                Changed();
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            lock (_drawFrameLock)
            {
                if (_visible == (e.WindowActivationState != CoreWindowActivationState.Deactivated))
                {
                    return;
                }

                _visible = e.WindowActivationState != CoreWindowActivationState.Deactivated;

                if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
                {
                    if (_shouldPlay && _source != null && _layoutRoot.IsLoaded)
                    {
                        Play();
                    }
                }
                else
                {
                    _shouldPlay = _subscribed;
                    Subscribe(false);
                }
            }
        }

        private void Changed(bool force = false)
        {
            if (_canvas == null)
            {
                // Load is going to invoke Changed again
                Load();
                return;
            }

            lock (_recreateLock)
            {
                var newDpi = _currentDpi;
                var newSize = _currentSize;

                bool needsCreate = (_surface == null);
                needsCreate |= (_surface?.Dpi != newDpi);
                needsCreate |= (_surface?.Size.Width != newSize.X || _surface?.Size.Height != newSize.Y);
                needsCreate |= force;

                if (needsCreate && newSize.X > 0 && newSize.Y > 0)
                {
                    try
                    {
                        _surface = new CanvasImageSource(CanvasDevice.GetSharedDevice(), newSize.X, newSize.Y, newDpi, CanvasAlphaMode.Premultiplied);
                        _canvas.Source = _surface;

                        DrawFrame();
                    }
                    catch
                    {
                        Unload();
                    }
                }
            }
        }

        protected bool Load()
        {
            if (_unloaded && _layoutRoot != null && _layoutRoot.IsLoaded)
            {
                while (_layoutRoot.Children.Count > 0)
                {
                    _layoutRoot.Children.Remove(_layoutRoot.Children[0]);
                }

                _canvas = new Image
                {
                    Stretch = Stretch.Fill,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                AutomationProperties.SetAccessibilityView(_canvas, AccessibilityView.Raw);

                _layoutRoot.Children.Add(_canvas);
                Changed();

                _unloaded = false;
                SourceChanged();

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
            RegisterEventHandlers();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
            UnregisterEventHandlers();
        }

        public async void Unload()
        {
            _shouldPlay = false;
            _unloaded = true;
            Subscribe(false);

            lock (_recreateLock)
            {
                //_canvas.Source = new BitmapImage();
                _layoutRoot.Children.Remove(_canvas);
                _canvas = null;

                _surface?.Device.Trim();
                _surface = null;
            }

            await _nextFrameLock.WaitAsync();
            await Task.Run(PrepareDispose);

            _nextFrameLock.Release();
        }

        private void PrepareDispose()
        {
            Dispose();

            lock (_drawFrameLock)
            {
                _bitmap?.Dispose();
                _bitmap = null;
            }
        }

        // For _source and _animation
        protected abstract void Dispose();

        private void OnRendering(object sender, object e)
        {
            lock (_drawFrameLock)
            {
                if (_interval.TotalMilliseconds > 17)
                {
                    var args = e as RenderingEventArgs;
                    var diff = args.RenderingTime - _elapsed;

                    if (diff < _interval / 3 * 2 || !_visible)
                    {
                        return;
                    }

                    _elapsed = args.RenderingTime;
                }

                DrawFrame();

                if (_unsubscribe)
                {
                    Subscribe(false);
                }
            }
        }

        public void DrawFrame()
        {
            if (_surface == null)
            {
                return;
            }

            try
            {
                using (var session = _surface.CreateDrawingSession(Colors.Transparent))
                {
                    if (_animation == null || _unloaded)
                    {
                        return;
                    }

                    if (_bitmap == null)
                    {
                        _bitmap = CreateBitmap(session);
                    }
                    else
                    {
                        DrawFrame(_surface, session);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_surface != null && _surface.Device.IsDeviceLost(ex.HResult))
                {
                    _surface = null;
                    _bitmap = null;

                    Changed();
                }
                else
                {
                    Unload();
                }
            }
        }

        protected abstract CanvasBitmap CreateBitmap(ICanvasResourceCreator sender);

        protected abstract void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args);

        private void PrepareNextFrame(ThreadPoolTimer timer)
        {
            //lock (_nextFrameLock)
            _nextFrameLock.Wait();
            {
                NextFrame();
            }
            _nextFrameLock.Release();
        }

        private void PrepareNextFrame()
        {
            //lock (_nextFrameLock)
            _nextFrameLock.Wait();
            {
                NextFrame();
            }
            _nextFrameLock.Release();
        }

        protected abstract void NextFrame();

        protected abstract void SourceChanged();

        protected async void OnSourceChanged()
        {
            if (AutoPlay || _shouldPlay)
            {
                _shouldPlay = false;
                Subscribe(true);
            }
            else
            {
                Subscribe(false);

                if (!_unloaded)
                {
                    _bitmap = CreateBitmap(CanvasDevice.GetSharedDevice());

                    // Invalidate to render the first frame
                    await Task.Run(PrepareNextFrame);
                    DrawFrame();
                }
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

            if (_subscribed || !_visible)
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

        protected void Subscribe(bool subscribe)
        {
            lock (_drawFrameLock)
            {
                if (Dispatcher.HasThreadAccess)
                {
                    if (subscribe)
                    {
                        _unsubscribe = false;
                    }

                    _subscribed = subscribe;

                    if (_timer != null)
                    {
                        _timer.Cancel();
                        _timer = null;
                    }

                    CompositionTarget.Rendering -= OnRendering;

                    if (subscribe)
                    {
                        _timer = ThreadPoolTimer.CreatePeriodicTimer(PrepareNextFrame, _interval);
                        CompositionTarget.Rendering += OnRendering;
                    }
                }
                else
                {
                    _unsubscribe = !subscribe;
                }
            }
        }

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get => (bool)GetValue(IsLoopingEnabledProperty);
            set => SetValue(IsLoopingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(AnimatedControl<TSource, TAnimation>), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedControl<TSource, TAnimation>)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimatedControl<TSource, TAnimation>), new PropertyMetadata(true));

        #endregion

        #region Stretch


        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Stretch.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(AnimatedControl<TSource, TAnimation>), new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedControl<TSource, TAnimation>)d)._stretch = (Stretch)e.NewValue;
        }

        #endregion
    }
}
