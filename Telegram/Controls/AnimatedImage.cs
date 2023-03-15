//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    // This is a lightweight fork of AnimatedControl
    // that just uses a WriteableBitmap rather than DirectX
    [TemplatePart(Name = "Canvas", Type = typeof(Image))]
    public abstract class AnimatedImage<TAnimation> : Control, IPlayerView
    {
        protected float _currentDpi;
        protected bool _active = true;
        protected bool _visible = true;

        private readonly bool _autoPause;

        private WriteableBitmap _bitmap0;
        private WriteableBitmap _bitmap1;

        private PixelBuffer _foregroundFrame;
        private readonly ConcurrentQueue<PixelBuffer> _backgroundQueue = new();

        private bool _bitmapClean = true;

        protected Panel _layoutRoot;
        protected Image _canvas;

        protected TAnimation _animation;

        protected bool? _playing;

        protected bool _subscribed;
        protected bool _unsubscribe;

        private bool _hasInitialLoadedEventFired;
        protected bool _unloaded;

        protected bool _isLoopingEnabled = true;

        protected readonly object _recreateLock = new();
        protected readonly object _drawFrameLock = new();
        protected readonly SemaphoreSlim _nextFrameLock = new(1, 1);

        protected TimeSpan _interval;
        protected TimeSpan _elapsed;

        // Better hardware detection?
        protected readonly bool _limitFps = true;

        protected readonly DispatcherQueue _dispatcher;
        private LoopThread _timer;

        protected AnimatedImage(bool? limitFps, bool autoPause = true)
        {
            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            _autoPause = autoPause;

            _limitFps = limitFps ?? !Windows.UI.Composition.CompositionCapabilities.GetForCurrentView().AreEffectsFast();
            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        protected override void OnApplyTemplate()
        {
            RegisterEventHandlers();

            var canvas = GetTemplateChild("Canvas") as Image;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;
            UpdateSource();

            _layoutRoot = GetTemplateChild("LayoutRoot") as Panel;
            _layoutRoot.Loaded += OnLoaded;
            _layoutRoot.Loading += OnLoading;
            _layoutRoot.Unloaded += OnUnloaded;

            OnLoaded();
            base.OnApplyTemplate();
        }

        private void RegisterEventHandlers()
        {
            _currentDpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            _active = !_autoPause || Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground;
            _visible = Window.Current.CoreWindow.Visible;

            if (_hasInitialLoadedEventFired)
            {
                return;
            }

            DisplayInformation.GetForCurrentView().DpiChanged += OnDpiChanged;
            Window.Current.VisibilityChanged += OnVisibilityChanged;

            if (_autoPause)
            {
                Window.Current.Activated += OnActivated;
            }

            _hasInitialLoadedEventFired = true;
        }

        private void UnregisterEventHandlers()
        {
            DisplayInformation.GetForCurrentView().DpiChanged -= OnDpiChanged;
            Window.Current.VisibilityChanged -= OnVisibilityChanged;

            if (_autoPause)
            {
                Window.Current.Activated -= OnActivated;
            }

            _hasInitialLoadedEventFired = false;
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            lock (_drawFrameLock)
            {
                _currentDpi = sender.LogicalDpi;
                Changed();
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            lock (_drawFrameLock)
            {
                if (_active == (e.WindowActivationState != CoreWindowActivationState.Deactivated))
                {
                    return;
                }

                _active = e.WindowActivationState != CoreWindowActivationState.Deactivated;

                if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
                {
                    OnSourceChanged();
                }
                else
                {
                    Subscribe(false);
                }
            }
        }

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            lock (_drawFrameLock)
            {
                if (_visible == e.Visible)
                {
                    return;
                }

                _visible = e.Visible;

                if (e.Visible)
                {
                    Changed();
                }
                else
                {
                    Subscribe(false);
                }
            }
        }

        private void Changed(bool force = false)
        {
            if (_canvas == null || !_visible)
            {
                // Load is going to invoke Changed again
                Load();
                return;
            }

            lock (_recreateLock)
            {
                var newDpi = _currentDpi;

                bool needsCreate = _bitmap0 == null;
                needsCreate |= _currentDpi != newDpi;
                needsCreate |= force;

                if (needsCreate)
                {
                    try
                    {
                        CreateBitmap();

                        Invalidate();
                        OnSourceChanged();
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
                UpdateSource();
                Changed();

                _unloaded = false;
                OnLoaded();

                return true;
            }

            return false;
        }

        private void UpdateSource()
        {
            //if (_canvas == null || _canvas.Source == _current)
            //{
            //    return;
            //}

            //OnUpdateSource(_current);
        }

        protected virtual void OnUpdateSource(WriteableBitmap bitmap)
        {
            //_canvas.Source = _current;
        }

        protected virtual void OnLoaded()
        {
            if (_animation != null)
            {
                OnSourceChanged();
            }
            else
            {
                SourceChanged();
            }
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
            if (IsLoaded)
            {
                return;
            }

            Unload();
            UnregisterEventHandlers();
        }

        public async void Unload()
        {
            _playing = null;
            _unloaded = true;
            Subscribe(false);

            lock (_recreateLock)
            {
                ////_canvas.Source = new BitmapImage();
                //_layoutRoot?.Children.Remove(_canvas);
                //_canvas = null;
                if (_canvas != null)
                {
                    _canvas.Source = null;
                }
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
                _foregroundFrame = null;
                _backgroundQueue.Clear();

                _bitmap0 = null;
                _bitmap1 = null;
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

                    if (diff < _interval / 3 * 2 || !_active)
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

        public void Invalidate()
        {
            lock (_drawFrameLock)
            {
                DrawFrame();
            }
        }

        protected void DrawFrame()
        {
            if (_animation == null || _unloaded || !_visible)
            {
                return;
            }

            if (_bitmap0 == null)
            {
                CreateBitmap();
            }
            else
            {
                var pixels = Interlocked.Exchange(ref _foregroundFrame, null);
                if (pixels != null)
                {
                    if (_canvas.Source is WriteableBitmap bitmap)
                    {
                        _backgroundQueue.Enqueue(new PixelBuffer(bitmap));
                    }

                    pixels.Visual.Invalidate();

                    _canvas.Source = pixels.Visual;
                    DrawFrame(pixels.Visual);
                }
            }
        }

        protected void CreateBitmap()
        {
            var temp = CreateBitmap(_currentDpi, out int width, out int height);

            var needsCreate = _bitmap0 == null || _bitmap1 == null;
            needsCreate |= _bitmap0?.PixelWidth != width || _bitmap0?.PixelHeight != height;
            needsCreate |= _bitmap1?.PixelWidth != width || _bitmap1?.PixelHeight != height;

            if (temp && needsCreate && _animation != null)
            {
                _bitmap0 = new WriteableBitmap(width, height);
                _bitmap1 = new WriteableBitmap(width, height);
                _bitmapClean = true;

                _foregroundFrame = null;
                _backgroundQueue.Clear();

                _backgroundQueue.Enqueue(new PixelBuffer(_bitmap0));
                _backgroundQueue.Enqueue(new PixelBuffer(_bitmap1));
            }

            UpdateSource();
        }

        protected abstract bool CreateBitmap(float dpi, out int width, out int height);

        protected SizeInt32 GetDpiAwareSize(Size size)
        {
            return GetDpiAwareSize(size.Width, size.Height);
        }

        protected SizeInt32 GetDpiAwareSize(double width, double height)
        {
            return new SizeInt32
            {
                Width = (int)(width * (_currentDpi / 96)),
                Height = (int)(height * (_currentDpi / 96))
            };
        }

        protected int GetDpiAwareSize(double size)
        {
            return (int)(size * (_currentDpi / 96));
        }

        protected abstract void DrawFrame(WriteableBitmap args);

        protected void PrepareNextFrame()
        {
            PrepareNextFrame(null, null);
        }

        private void PrepareNextFrame(object sender, object e)
        {
            if (_nextFrameLock.Wait(0))
            {
                if (TryDequeueOrExchange(out PixelBuffer frame))
                {
                    if (NextFrame(frame))
                    {
                        var dropped = Interlocked.Exchange(ref _foregroundFrame, frame);
                        if (dropped != null)
                        {
                            _backgroundQueue.Enqueue(dropped);
                        }
                    }
                    else
                    {
                        _backgroundQueue.Enqueue(frame);
                    }

                    _bitmapClean = false;
                }

                _nextFrameLock.Release();
            }
        }

        private bool TryDequeueOrExchange(out PixelBuffer frame)
        {
            if (_backgroundQueue.TryDequeue(out frame))
            {
                return true;
            }

            // TODO: this looks quite dangerous...
            frame = Interlocked.Exchange(ref _foregroundFrame, null);
            return frame != null;
        }

        protected abstract bool NextFrame(PixelBuffer pixels);

        protected abstract void SourceChanged();

        protected async void OnSourceChanged()
        {
            var playing = AutoPlay ? _playing != false : _playing == true;
            if (playing && _active)
            {
                Play(false);
            }
            else
            {
                Subscribe(false);

                if (playing && _bitmapClean && !_unloaded)
                {
                    CreateBitmap();

                    // Invalidate to render the first frame
                    // Would be nice to move this to IndividualAnimatedImage
                    // but this isn't currently possible
                    await Task.Run(PrepareNextFrame);
                    Invalidate();
                }
            }
        }

        public bool Play()
        {
            return Play(true);
        }

        private bool Play(bool tryLoad)
        {
            _playing = true;

            if (tryLoad)
            {
                Load();
            }

            if (_canvas == null || _animation == null || _subscribed || !_active)
            {
                return false;
            }

            if (_canvas.IsLoaded || _layoutRoot.IsLoaded)
            {
                if (tryLoad is false)
                {
                    CreateBitmap();
                }

                Subscribe(true);
            }

            return true;
        }

        public void Pause()
        {
            var canvas = _canvas;
            if (canvas == null)
            {
                return;
            }

            _playing = false;
            Subscribe(false);
        }

        protected void Subscribe(bool subscribe)
        {
            lock (_drawFrameLock)
            {
                if (Dispatcher.HasThreadAccess)
                {
                    if (subscribe && _active)
                    {
                        _unsubscribe = false;
                        OnSubscribe(subscribe);

                        CompositionTarget.Rendering -= OnRendering;
                        CompositionTarget.Rendering += OnRendering;
                    }
                    else
                    {
                        OnSubscribe(subscribe);

                        CompositionTarget.Rendering -= OnRendering;
                    }

                    _subscribed = subscribe && _active;
                }
                else
                {
                    if (subscribe)
                    {
                        // This theoretically never happens because Subscribe
                        // never gets invoked from a background thread
                        return;
                    }

                    _unsubscribe = true;
                    OnSubscribe(subscribe);
                }
            }
        }

        protected void OnSubscribe(bool subscribe)
        {
            if (subscribe)
            {
                if (_timer == null)
                {
                    _timer = LoopThreadPool.Get(_interval);
                    _timer.Tick += PrepareNextFrame;
                }
            }
            else if (_timer != null)
            {
                _timer.Tick -= PrepareNextFrame;
                _timer = null;
            }
        }

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get => (bool)GetValue(IsLoopingEnabledProperty);
            set => SetValue(IsLoopingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(AnimatedImage<TAnimation>), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage<TAnimation>)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimatedImage<TAnimation>), new PropertyMetadata(true, OnAutoPlayChanged));

        private static void OnAutoPlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage<TAnimation>)d).OnAutoPlayChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        protected virtual void OnAutoPlayChanged(bool newValue, bool oldValue)
        {

        }

        #endregion

        #region Stretch

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(AnimatedImage<TAnimation>), new PropertyMetadata(Stretch.Uniform));

        #endregion
    }
}
