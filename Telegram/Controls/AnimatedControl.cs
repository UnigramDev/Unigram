//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Buffers;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(Image))]
    public abstract class AnimatedControl<TAnimation> : Control, IPlayerView
    {
        protected Vector2 _currentSize;
        protected float _currentDpi;
        protected bool _active = true;
        protected bool _visible = true;

        private readonly bool _autoPause;

        protected volatile bool _disposed;

        protected CanvasImageSource _surface;
        protected CanvasBitmap _bitmap;

        protected Panel _layoutRoot;
        protected Image _canvas;

        protected TAnimation _animation;

        protected bool? _playing;

        protected bool _subscribed;
        protected bool _unsubscribe;

        protected bool _hasInitialLoadedEventFired;
        protected bool _unloaded;

        protected bool _isLoopingEnabled = true;
        protected Stretch _stretch = Stretch.Uniform;

        protected readonly object _recreateLock = new();
        protected readonly object _drawFrameLock = new();
        protected readonly SemaphoreSlim _nextFrameLock = new(1, 1);

        protected TimeSpan _interval;
        protected TimeSpan _elapsed;

        // Better hardware detection?
        protected readonly bool _limitFps = true;

        protected readonly DispatcherQueue _dispatcher;

        protected AnimatedControl(bool? limitFps, bool autoPause = true)
        {
            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            _autoPause = autoPause;

            _limitFps = limitFps ?? !Windows.UI.Composition.CompositionCapabilities.GetForCurrentView().AreEffectsFast();
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            SizeChanged += OnSizeChanged;
            RegisterEventHandlers();
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild("Canvas") as Image;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;

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
            CompositionTarget.SurfaceContentsLost += OnSurfaceContentsLost;

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
            CompositionTarget.SurfaceContentsLost -= OnSurfaceContentsLost;

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
                OnDpiChanged(sender.LogicalDpi);
            }
        }

        protected virtual void OnDpiChanged(float currentDpi)
        {

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

                _bitmap?.Dispose();
                _bitmap = null;

                OnSurfaceContentsLost();
                Changed();
            }
        }

        protected virtual void OnSurfaceContentsLost()
        {

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

        private void Changed(bool force = false, bool load = true)
        {
            if (_canvas == null || !_visible)
            {
                if (load)
                {
                    // Load is going to invoke Changed again
                    Load();
                }

                return;
            }

            lock (_recreateLock)
            {
                var newDpi = _currentDpi;
                var newSize = _currentSize;

                bool needsCreate = _surface == null;
                needsCreate |= _surface?.Dpi != newDpi;
                needsCreate |= _surface?.Size.Width != newSize.X || _surface?.Size.Height != newSize.Y;
                needsCreate |= force;

                if (needsCreate && newSize.X > 0 && newSize.Y > 0)
                {
                    try
                    {
                        var device = _surface?.Device ?? CanvasDevice.GetSharedDevice();

                        _surface = new CanvasImageSource(device, newSize.X, newSize.Y, newDpi, CanvasAlphaMode.Premultiplied);
                        _canvas.Source = _surface;

                        CreateBitmap(true);

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
                Changed(load: false);

                _unloaded = false;
                _disposed = false;
                OnLoaded();

                return true;
            }

            return false;
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
                //_canvas.Source = new BitmapImage();
                _layoutRoot?.Children.Remove(_canvas);
                _canvas = null;

                _surface = null;
            }

            await _nextFrameLock.WaitAsync();
            await PrepareDisposeAsync();

            _nextFrameLock.Release();
        }

        private async Task PrepareDisposeAsync()
        {
            _disposed = true;

            await Task.Run(Dispose);

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
            if (_surface == null || !_visible)
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

                    CreateBitmap(false);
                    DrawFrame(_surface, session);
                }
            }
            catch (Exception ex)
            {
                HandleDeviceLost(ex);
            }
        }

        private async void CreateBitmap(bool force)
        {
            try
            {
                var device = _surface?.Device;
                if (device != null && (_bitmap == null || force))
                {
                    var temp = CreateBitmap(device);
                    if (temp != null)
                    {
                        var dispose = _bitmap;
                        _bitmap = temp;

                        if (dispose != null)
                        {
                            await _nextFrameLock.WaitAsync();
                            dispose.Dispose();
                            _nextFrameLock.Release();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleDeviceLost(ex);
            }
        }

        private void HandleDeviceLost(Exception ex)
        {
            if (_surface != null && _surface.Device.IsDeviceLost(ex.HResult))
            {
                _surface = null;

                _bitmap?.Dispose();
                _bitmap = null;

                OnSurfaceContentsLost();
                Changed();
            }
            else
            {
                Unload();
            }
        }

        protected abstract CanvasBitmap CreateBitmap(CanvasDevice device);

        protected CanvasBitmap CreateBitmap(CanvasDevice device, int width, int height, DirectXPixelFormat pixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized, float dpi = 96)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(width * height * 4);
            var bitmap = CanvasBitmap.CreateFromBytes(device, buffer, width, height, pixelFormat, dpi);
            ArrayPool<byte>.Shared.Return(buffer);

            return bitmap;
        }

        protected CanvasRenderTarget CreateTarget(ICanvasResourceCreator sender, int width, int height, DirectXPixelFormat pixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized, float dpi = 96)
        {
            return new CanvasRenderTarget(sender, width, height, dpi, pixelFormat, CanvasAlphaMode.Premultiplied);
        }

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

        protected abstract void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args);

        protected void PrepareNextFrame()
        {
            if (_nextFrameLock.Wait(0))
            {
                if (_disposed)
                {
                    return;
                }

                NextFrame();
                _nextFrameLock.Release();
            }
        }

        protected abstract void NextFrame();

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

                if (_playing == null && !_unloaded)
                {
                    CreateBitmap(false);

                    // Invalidate to render the first frame
                    // Would be nice to move this to IndividualAnimatedControl
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
                    CreateBitmap(false);
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

        protected abstract void OnSubscribe(bool subscribe);

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get => (bool)GetValue(IsLoopingEnabledProperty);
            set => SetValue(IsLoopingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(AnimatedControl<TAnimation>), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedControl<TAnimation>)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimatedControl<TAnimation>), new PropertyMetadata(true, OnAutoPlayChanged));

        private static void OnAutoPlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedControl<TAnimation>)d).OnAutoPlayChanged((bool)e.NewValue, (bool)e.OldValue);
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

        // Using a DependencyProperty as the backing store for Stretch.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(AnimatedControl<TAnimation>), new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedControl<TAnimation>)d)._stretch = (Stretch)e.NewValue;
        }

        #endregion
    }

    public abstract class IndividualAnimatedControl<TAnimation> : AnimatedControl<TAnimation>
    {
        private LoopThread _timer;

        protected IndividualAnimatedControl(bool? limitFps, bool autoPause = true)
            : base(limitFps, autoPause)
        {
        }

        protected override void OnSubscribe(bool subscribe)
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

        private void PrepareNextFrame(object sender, object e)
        {
            if (_nextFrameLock.Wait(0))
            {
                if (_disposed)
                {
                    return;
                }

                NextFrame();
                _nextFrameLock.Release();
            }
        }
    }
}
