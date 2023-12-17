using Microsoft.Graphics.Canvas.Effects;
using RLottie;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public class AnimatedImagePositionChangedEventArgs : EventArgs
    {
        public int Position { get; set; }
    }

    public class AnimatedImageLoopCompletedEventArgs : CancelEventArgs
    {

    }

    public class AnimatedImage : ControlEx, IPlayerView
    {
        enum PlayingState
        {
            None,
            Playing,
            Paused
        }

        private bool _templateApplied;

        private PlayingState _state;
        private bool _delayedPlay;

        private double _rasterizationScale;

        private AnimatedImagePresenter _presenter;
        private int _suppressEvents;

        private CompositionAnimation _shimmer;

        protected bool _clean = true;

        public AnimatedImage()
        {
            DefaultStyleKey = typeof(AnimatedImage);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        public event EventHandler Ready;
        public event EventHandler<AnimatedImagePositionChangedEventArgs> PositionChanged;
        public event EventHandler<AnimatedImageLoopCompletedEventArgs> LoopCompleted;

        protected readonly struct SuppressEventsDisposable : IDisposable
        {
            private readonly AnimatedImage _owner;

            public SuppressEventsDisposable(AnimatedImage owner)
            {
                _owner = owner;
                ++_owner._suppressEvents;
            }

            public void Dispose()
            {
                --_owner._suppressEvents;
                _owner.Load();
            }
        }

        public IDisposable BeginBatchUpdate()
        {
            return new SuppressEventsDisposable(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Load();

            XamlRoot.Changed += OnRasterizationScaleChanged;
            ReplacementColor?.RegisterColorChangedCallback(OnReplacementColorChanged, ref _replacementColorToken);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();

            XamlRoot.Changed -= OnRasterizationScaleChanged;
            ReplacementColor?.UnregisterColorChangedCallback(ref _replacementColorToken);
        }

        public bool IsPlaying => _delayedPlay || _state == PlayingState.Playing;

        public void Play()
        {
            if (_presenter != null)
            {
                _delayedPlay = false;

                if (_state != PlayingState.Playing)
                {
                    _state = PlayingState.Playing;
                    _presenter.Play(this);
                }
            }
            else
            {
                _delayedPlay = true;
            }
        }

        public void Pause()
        {
            _delayedPlay = false;

            if (_presenter != null)
            {
                if (_state == PlayingState.Playing)
                {
                    _state = PlayingState.Paused;
                    _presenter.Pause();
                }
            }
        }

        public void Seek(string marker)
        {
            _presenter?.Seek(marker);
        }

        #region IsViewportAware

        public bool IsViewportAware
        {
            get { return (bool)GetValue(IsViewportAwareProperty); }
            set { SetValue(IsViewportAwareProperty, value); }
        }

        public static readonly DependencyProperty IsViewportAwareProperty =
            DependencyProperty.Register("IsViewportAware", typeof(bool), typeof(AnimatedImage), new PropertyMetadata(false, OnViewportAwareChanged));

        private static void OnViewportAwareChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage)d).OnViewportAwareChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnViewportAwareChanged(bool newValue, bool oldValue)
        {
            if (newValue)
            {
                EffectiveViewportChanged += OnEffectiveViewportChanged;
            }
            else
            {
                EffectiveViewportChanged -= OnEffectiveViewportChanged;
            }
        }

        private bool _withinViewport;

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var within = args.BringIntoViewDistanceX < sender.ActualWidth && args.BringIntoViewDistanceY < sender.ActualHeight;
            if (within && !_withinViewport)
            {
                _withinViewport = true;
                Play();
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                Pause();
            }
        }

        public void ViewportChanged(bool within)
        {
            if (within && !_withinViewport)
            {
                _withinViewport = true;
                Play();
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                Pause();
            }
        }

        #endregion



        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(AnimatedImage), new PropertyMetadata(null, OnPropertyChanged));

        #endregion

        #region LoopCount

        public int LoopCount
        {
            get { return (int)GetValue(LoopCountProperty); }
            set { SetValue(LoopCountProperty, value); }
        }

        public static readonly DependencyProperty LoopCountProperty =
            DependencyProperty.Register("LoopCount", typeof(int), typeof(AnimatedImage), new PropertyMetadata(0, OnPropertyChanged));

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimatedImage), new PropertyMetadata(false, OnPropertyChanged));

        #endregion

        #region LimitFps

        public bool LimitFps
        {
            get { return (bool)GetValue(LimitFpsProperty); }
            set { SetValue(LimitFpsProperty, value); }
        }

        public static readonly DependencyProperty LimitFpsProperty =
            DependencyProperty.Register("LimitFps", typeof(bool), typeof(AnimatedImage), new PropertyMetadata(false, OnPropertyChanged));

        #endregion

        #region IsCachingEnabled

        public bool IsCachingEnabled
        {
            get => (bool)GetValue(IsCachingEnabledProperty);
            set => SetValue(IsCachingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsCachingEnabledProperty =
            DependencyProperty.Register("IsCachingEnabled", typeof(bool), typeof(AnimatedImage), new PropertyMetadata(true, OnPropertyChanged));

        #endregion

        #region FrameSize

        public Size FrameSize
        {
            get => (Size)GetValue(FrameSizeProperty);
            set => SetValue(FrameSizeProperty, value);
        }

        public static readonly DependencyProperty FrameSizeProperty =
            DependencyProperty.Register("FrameSize", typeof(Size), typeof(AnimatedImage), new PropertyMetadata(new Size(256, 256), OnPropertyChanged));

        #endregion

        #region DecodeFrameType

        public DecodePixelType DecodeFrameType
        {
            get { return (DecodePixelType)GetValue(DecodeFrameTypeProperty); }
            set { SetValue(DecodeFrameTypeProperty, value); }
        }

        public static readonly DependencyProperty DecodeFrameTypeProperty =
            DependencyProperty.Register("DecodeFrameType", typeof(DecodePixelType), typeof(AnimatedImage), new PropertyMetadata(DecodePixelType.Physical, OnPropertyChanged));

        #endregion





        #region Stretch

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register("Stretch", typeof(Stretch), typeof(AnimatedImage), new PropertyMetadata(Stretch.Uniform));

        #endregion

        private AnimatedImagePresentation GetPresentation()
        {
            if (Source != null)
            {
                var width = (int)FrameSize.Width;
                var height = (int)FrameSize.Height;

                if (DecodeFrameType == DecodePixelType.Logical)
                {
                    width = (int)(width * _rasterizationScale);
                    height = (int)(height * _rasterizationScale);
                }

                return new AnimatedImagePresentation(Source, width, height, LimitFps, LoopCount, AutoPlay, IsCachingEnabled);
            }

            return null;
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage)d).Load();
        }

        private void Load()
        {
            if (_suppressEvents > 0)
            {
                return;
            }

            if (_templateApplied && IsConnected)
            {
                var presentation = GetPresentation();
                if (presentation != _presenter?.Presentation)
                {
                    if (_presenter != null)
                    {
                        _presenter.Unload(this, _state == PlayingState.Playing);
                        _presenter.LoopCompleted -= LoopCompleted;
                        _presenter.PositionChanged -= PositionChanged;
                        _presenter.Paused -= OnPaused;
                        _presenter = null;
                    }

                    _delayedPlay |= _state == PlayingState.Playing;
                    _state = PlayingState.None;

                    UpdateShimmer(presentation?.Source);

                    if (presentation != null)
                    {
                        _clean = true;
                        _presenter = AnimatedImageLoader.Current.GetOrCreate(presentation);
                        _presenter.LoopCompleted += LoopCompleted;
                        _presenter.PositionChanged += PositionChanged;
                        _presenter.Paused += OnPaused;
                        _presenter.Load(this);

                        if (_delayedPlay)
                        {
                            Play();
                        }
                    }
                }
            }
        }

        private void UpdateShimmer(AnimatedImageSource source)
        {
            if (source?.Outline != null)
            {
                _shimmer = CompositionPathParser.ParseThumbnail(source.Width, source.Height, source.Outline, out ShapeVisual visual, IsOutlineAnimated);
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, visual);
            }
            else
            {
                _shimmer = null;
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, null);
            }
        }

        private void Unload()
        {
            if (_presenter != null && !IsConnected)
            {
                _presenter.Unload(this, _state == PlayingState.Playing);
                _presenter.LoopCompleted -= LoopCompleted;
                _presenter.PositionChanged -= PositionChanged;
                _presenter.Paused -= OnPaused;
                _presenter = null;
            }
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _delayedPlay = false;
            _state = PlayingState.Paused;
        }

        public virtual void Invalidate(ImageSource source)
        {
            Canvas.ImageSource = source;

            if (_clean && source != null)
            {
                _clean = false;

                Ready?.Invoke(this, EventArgs.Empty);
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, null);

                if (ReplacementColor != null)
                {
                    ReplacementColorChanged(true);
                }
            }
        }

        private ImageBrush Canvas;
        private FrameworkElement LayoutRoot;

        protected override void OnApplyTemplate()
        {
            //Logger.Debug();
            Canvas = GetTemplateChild(nameof(Canvas)) as ImageBrush;
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as FrameworkElement;

            if (Canvas != null)
            {
                Canvas.Stretch = Stretch;
            }

            _templateApplied = true;
            _rasterizationScale = XamlRoot.RasterizationScale;

            Load();
            ReplacementColorChanged();
            base.OnApplyTemplate();
        }

        private void OnRasterizationScaleChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            if (_rasterizationScale != sender.RasterizationScale && DecodeFrameType == DecodePixelType.Logical)
            {
                _rasterizationScale = sender.RasterizationScale;
                Load();
            }
        }

        #region ReplacementColor

        private bool _needsBrushUpdate;
        private Color _replacementColor;
        private long _replacementColorToken;
        private CompositionEffectFactory _effectFactory;

        // Implemented as Brush so that we can receive Color changed updates
        public Brush ReplacementColor
        {
            get { return (Brush)GetValue(ReplacementColorProperty); }
            set { SetValue(ReplacementColorProperty, value); }
        }

        public static readonly DependencyProperty ReplacementColorProperty =
            DependencyProperty.Register("ReplacementColor", typeof(Brush), typeof(AnimatedImage), new PropertyMetadata(null, OnReplacementColorChanged));

        private static void OnReplacementColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage)d).OnReplacementColorChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnReplacementColorChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            oldValue?.UnregisterColorChangedCallback(ref _replacementColorToken);
            newValue?.RegisterColorChangedCallback(OnReplacementColorChanged, ref _replacementColorToken);

            ReplacementColorChanged();
        }

        private void OnReplacementColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            ReplacementColorChanged();
        }

        protected void ReplacementColorChanged(bool fast = false)
        {
            if (_needsBrushUpdate)
            {
                return;
            }
            else if (fast)
            {
                UpdateBrush();
                return;
            }

            _needsBrushUpdate = true;
            VisualUtilities.QueueCallbackForCompositionRendering(UpdateBrush);
        }

        private void UpdateBrush()
        {
            _needsBrushUpdate = false;

            if (LayoutRoot == null)
            {
                return;
            }
            
            if (ReplacementColor is not SolidColorBrush replacement || _presenter?.Presentation.Source.NeedsRepainting is not true)
            {
                if (_effectFactory != null)
                {
                    LayoutRoot.Opacity = 1;
                    ElementCompositionPreview.SetElementChildVisual(this, null);
                }

                _effectFactory = null;
                return;
            }

            // This code mostly comes from MonochromaticOverlayPresenter

            if (replacement.Color != _replacementColor)
            {
                _replacementColor = replacement.Color;
                _effectFactory = null;
            }

            var compositor = Window.Current.Compositor;

            if (_effectFactory == null)
            {
                // Build an effect that takes the source image and uses the alpha channel and replaces all other channels with
                // the ReplacementColor's RGB.
                var colorMatrixEffect = new ColorMatrixEffect();
                colorMatrixEffect.Source = new CompositionEffectSourceParameter("source");
                var colorMatrix = new Matrix5x4();

                // If the ReplacementColor is not transparent then use the RGB values as the new color. Otherwise
                // just show the target by using an Identity colorMatrix.
                if (_replacementColor.A != 0)
                {
                    colorMatrix.M51 = (float)(_replacementColor.R / 255.0);
                    colorMatrix.M52 = (float)(_replacementColor.G / 255.0);
                    colorMatrix.M53 = (float)(_replacementColor.B / 255.0);
                    colorMatrix.M44 = 1;
                }
                else
                {
                    colorMatrix.M11 = colorMatrix.M22 = colorMatrix.M33 = colorMatrix.M44 = 1;
                }
                colorMatrixEffect.ColorMatrix = colorMatrix;

                _effectFactory = compositor.CreateEffectFactory(colorMatrixEffect);
            }

            var actualSize = FrameSize.ToVector2();
            var offset = Vector2.Zero;

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            surfaceBrush.Stretch = CompositionStretch.None;
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            surface.SourceOffset = offset;
            surface.SourceSize = actualSize;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.None;
            var compBrush = _effectFactory.CreateBrush();
            compBrush.SetSourceParameter("source", surfaceBrush);

            var visual = compositor.CreateSpriteVisual();
            visual.Size = actualSize;
            visual.Brush = compBrush;

            LayoutRoot.Opacity = 0;
            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        #endregion

        #region IsOutlineAnimated

        public bool IsOutlineAnimated
        {
            get { return (bool)GetValue(IsOutlineAnimatedProperty); }
            set { SetValue(IsOutlineAnimatedProperty, value); }
        }

        public static readonly DependencyProperty IsOutlineAnimatedProperty =
            DependencyProperty.Register("IsOutlineAnimated", typeof(bool), typeof(AnimatedImage), new PropertyMetadata(null));

        #endregion
    }

    public class AnimatedImagePresenter
    {
        private bool _ticking;
        private bool _rendering;
        private bool _disposing;
        private bool _disposed;

        private int _loaded;

        private long _tracker;

        private int _playing;
        private bool _idle = true;

        // TODO: move this to PixelBuffer
        private bool _dirty;

        private bool _activated;

        private int _loopCount;

        private LoopThread _timer;
        private bool _timerSubscribed;
        private bool _renderingSubscribed;

        private readonly AnimatedImagePresentation _presentation;
        private readonly AnimatedImageLoader _loader;

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly FifoActionWorker _workerQueue;

        private readonly List<AnimatedImage> _images = new();

        private AnimatedImageTask _task;
        private readonly object _lock = new();

        private AnimatedImagePositionChangedEventArgs _prevPosition;
        private int _nextPosition;

        private AnimatedImageLoopCompletedEventArgs _prevCompleted;

        private string _nextMarker;

        public AnimatedImagePresenter(AnimatedImageLoader loader, DispatcherQueue dispatcherQueue, AnimatedImagePresentation configuration)
        {
            _presentation = configuration;
            _loader = loader;

            _dispatcherQueue = dispatcherQueue;
            _workerQueue = new FifoActionWorker();

            Increment();
        }

        public bool Increment()
        {
            return Interlocked.Increment(ref _tracker) > 1;
        }

        public event EventHandler<AnimatedImagePositionChangedEventArgs> PositionChanged;
        public event EventHandler<AnimatedImageLoopCompletedEventArgs> LoopCompleted;

        public event EventHandler Paused;

        public AnimatedImagePresentation Presentation => _presentation;

        public int CorrelationId { get; set; }

        public void Load(AnimatedImage canvas)
        {
            _images.Add(canvas);
            _workerQueue.Run(PrepareImpl);

            if (_dirty)
            {
                canvas.Invalidate(_foregroundPrev?.Source);
            }
        }

        public void Unload(AnimatedImage canvas, bool playing)
        {
            _images.Remove(canvas);
            canvas.Invalidate(null);

            _workerQueue.Run(() => UnloadImpl(playing));
        }

        private void PrepareImpl()
        {
            lock (_lock)
            {
                _loaded++;

                if (_loaded == 1)
                {
                    if (_presentation.Source is DelayedFileSource delayed && !delayed.IsDownloadingCompleted)
                    {
                        //Logger.Debug("Loaded, delayed");
                        delayed.DownloadFile(this, UpdateFile);
                    }
                    else
                    {
                        //Logger.Debug("Loaded, requesting");
                        _loader.Load(this);
                    }
                }
            }
        }

        private void UpdateFile(object target, Td.Api.File file)
        {
            lock (_lock)
            {
                if (_loaded > 0)
                {
                    _loader.Load(this);
                }
            }
        }

        private void UnloadImpl(bool playing)
        {
            //Logger.Debug();

            lock (_lock)
            {
                _loaded--;

                if (playing)
                {
                    _playing--;
                }

                var tracker = Interlocked.Decrement(ref _tracker);

                if (_loaded <= 0 && tracker == 0)
                {
                    if (_task != null)
                    {
                        if (_ticking)
                        {
                            //Logger.Debug("Task exists, and timer is attached");
                            _disposing = true;
                            _ticking = false;
                        }
                        else
                        {
                            //Logger.Debug("Task exists, and timer is not attached");
                            Dispose();
                        }
                    }
                    else if (CorrelationId != 0)
                    {
                        _loader.Remove(CorrelationId);
                    }
                    else if (_presentation.Source is DelayedFileSource delayed)
                    {
                        delayed.Complete();
                    }
                }
            }
        }

        public void Play(AnimatedImage canvas)
        {
            _workerQueue.Run(PlayImpl);

            if (_dirty)
            {
                canvas.Invalidate(_foregroundPrev?.Source);
            }
        }

        public void Pause()
        {
            _workerQueue.Run(PauseImpl);
        }

        public void Seek(string marker)
        {
            _workerQueue.Run(() => SeekImpl(marker));
        }

        private void PlayImpl()
        {
            lock (_lock)
            {
                _playing++;
                _idle = false;

                if (_playing == 1 && !_ticking && _loopCount >= 0)
                {
                    if (_task == null)
                    {
                        if (_presentation.Source is DelayedFileSource delayed && !delayed.IsDownloadingCompleted)
                        {
                            //Logger.Debug("Loaded, delayed");
                            delayed.DownloadFile(this, UpdateFile);
                        }
                        else
                        {
                            //Logger.Debug("Loaded, requesting");
                            _loader.Load(this);
                        }

                        return;
                    }

                    if (_nextMarker != null)
                    {
                        _task.Seek(_nextMarker);
                        _nextMarker = null;
                    }

                    _rendering = true;
                    _dispatcherQueue.TryEnqueue(RegisterRendering);

                    _ticking = _activated;

                    if (!_timerSubscribed)
                    {
                        _timerSubscribed = true;
                        _timer.Tick += OnTick;
                    }
                }
            }
        }

        private void PauseImpl()
        {
            lock (_lock)
            {
                _playing--;
                _idle = false;

                if (_playing == 0)
                {
                    _ticking = false;
                }
            }
        }

        private void SeekImpl(string marker)
        {
            lock (_lock)
            {
                _nextMarker = marker;
                _loopCount = 0;
            }

            PauseImpl();
            PlayImpl();
        }

        public void Ready(AnimatedImageTask task)
        {
            _workerQueue.Run(() => ReadyImpl(task));
        }

        private void ReadyImpl(AnimatedImageTask task)
        {
            //Logger.Debug();

            lock (_lock)
            {
                var tracker = Interlocked.Read(ref _tracker);

                if (_loaded > 0)
                {
                    _timer = LoopThreadPool.Rent(task.Interval);
                    _task = task;

                    _rendering = true;

                    _dispatcherQueue.TryEnqueue(CreateResources);
                    _createdResourcesLock.Wait();

                    _ticking = (_idle && _presentation.AutoPlay) || (_playing > 0 && (_activated || _presentation.LoopCount > 0));
                    _idle = false;

                    if (!_timerSubscribed)
                    {
                        _timerSubscribed = true;
                        _timer.Tick += OnTick;
                    }
                }
                else if (_task != null && tracker == 0)
                {
                    if (_ticking)
                    {
                        //Logger.Debug("Task exists, and timer is attached");
                        _disposing = true;
                        _ticking = false;
                    }
                    else
                    {
                        //Logger.Debug("Task exists, and timer is not attached");
                        Dispose();
                    }
                }
            }
        }

        #region Resources

        private PixelBuffer _foregroundPrev;
        private PixelBuffer _foregroundNext;
        private PixelBuffer _backgroundNext;

        private readonly SemaphoreSlim _createdResourcesLock = new(0, 1);
        private readonly SemaphoreSlim _pausedLock = new(0, 1);

        private void CreateResources()
        {
            var width = _task.PixelWidth;
            var height = _task.PixelHeight;

            _foregroundPrev = new PixelBuffer(new WriteableBitmap(width, height));
            _backgroundNext = new PixelBuffer(new WriteableBitmap(width, height));

            _activated = Window.Current.CoreWindow.ActivationMode != CoreWindowActivationMode.Deactivated;

            _createdResourcesLock.Release();

            RegisterEvents();
            RegisterRendering();
        }

        private void RegisterEvents()
        {
            WindowContext.Current.Activated += OnActivated;
        }

        private void UnregisterEvents()
        {
            WindowContext.Current.Activated -= OnActivated;
        }

        private void InvokePaused()
        {
            Paused?.Invoke(this, EventArgs.Empty);
            _pausedLock.Release();
        }

        private async void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_disposed)
            {
                UnregisterEvents();
                return;
            }

            var activated = args.WindowActivationState != CoreWindowActivationState.Deactivated;
            var subscribe = await Task.Run(() => Activated(activated));

            if (subscribe)
            {
                RegisterRendering();
            }
        }

        public bool Activated(bool active)
        {
            lock (_lock)
            {
                if (_activated != active)
                {
                    _activated = active;

                    if (_playing > 0 && !active)
                    {
                        _ticking = false;
                    }
                    else if (_task != null && _playing > 0 && !_ticking && _loopCount >= 0 && active)
                    {
                        //_dispatcherQueue.TryEnqueue(RegisterRendering);

                        _rendering = true;
                        _ticking = true;

                        if (!_timerSubscribed)
                        {
                            _timerSubscribed = true;
                            _timer.Tick += OnTick;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private void RegisterRendering()
        {
            if (!_renderingSubscribed)
            {
                _renderingSubscribed = true;
                AnimatedImageLoader.Current.Rendering += OnRendering;
            }
        }

        #endregion

        private void OnTick(object sender, EventArgs e)
        {
            //Logger.Debug();

            lock (_lock)
            {
                OnTick(sender as LoopThread);
            }
        }

        private void OnTick(LoopThread sender)
        {
            if (_loaded > 0 && !_disposing && !_disposed)
            {
                NextFrame();
            }

            if (!_ticking)
            {
                //Logger.Debug("-=");

                _rendering = false;
                _timerSubscribed = false;
                sender.Tick -= OnTick;

                if (_disposing)
                {
                    Dispose();
                }
            }
        }

        #region Next frame

        private void NextFrame()
        {
            var frame = Interlocked.Exchange(ref _backgroundNext, null);
            if (frame != null)
            {
                if (NextFrame(frame))
                {
                    var dropped = Interlocked.Exchange(ref _foregroundNext, frame);
                    if (dropped != null)
                    {
                        Interlocked.Exchange(ref _backgroundNext, dropped);
                    }
                }
                else
                {
                    Interlocked.Exchange(ref _backgroundNext, frame);
                }
            }
        }

        private bool NextFrame(PixelBuffer frame)
        {
            var state = _task.NextFrame(frame, out _nextPosition);

            if (state == AnimatedImageTaskState.Stop)
            {
                _loopCount = -1;
                _ticking = false;
            }
            else if (state == AnimatedImageTaskState.Loop)
            {
                _loopCount++;

                _prevCompleted ??= new();
                _prevCompleted.Cancel = false;

                LoopCompleted?.Invoke(this, _prevCompleted);

                if (_prevCompleted.Cancel || (_loopCount >= _presentation.LoopCount && _presentation.LoopCount > 0))
                {
                    _ticking = false;
                    _loopCount = 0;

                    _dispatcherQueue.TryEnqueue(InvokePaused);
                    _pausedLock.Wait();
                    _playing = 0;
                }
            }

            return state != AnimatedImageTaskState.Skip;
        }

        #endregion

        private void Dispose()
        {
            //Logger.Debug();
            //Debug.Assert(_images.Count == 0);

            _dispatcherQueue.TryEnqueue(UnregisterEvents);
            LoopThreadPool.Release(_timer);

            _task = null;
            _timer = null;
            _disposing = false;
            _disposed = true;

            _foregroundPrev = null;
            _foregroundNext = null;
            _backgroundNext = null;

            _loader.Remove(_presentation);
        }

        protected TimeSpan _interval;
        protected TimeSpan _elapsed;

        private void OnRendering(object sender, object e)
        {
            //Logger.Debug();

            OnRendering(e);

            if (!_rendering && _renderingSubscribed)
            {
                //Logger.Debug("-=");
                _renderingSubscribed = false;
                AnimatedImageLoader.Current.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object e)
        {
            if (_interval.TotalMilliseconds > 17)
            {
                var args = e as RenderingEventArgs;
                var diff = args.RenderingTime - _elapsed;

                if (diff < _interval / 3 * 2 /*|| !_active*/)
                {
                    return;
                }

                _elapsed = args.RenderingTime;
            }

            DrawFrame();
        }

        private void DrawFrame()
        {
            // TODO: there is a chance that, if the animation has a single frame this will
            // pick the empty frame instead of the drawn one and thus the control will be blank

            var next = Interlocked.Exchange(ref _foregroundNext, null);
            if (next != null)
            {
                if (_foregroundPrev != null)
                {
                    Interlocked.Exchange(ref _backgroundNext, _foregroundPrev);
                }

                _dirty = true;
                _foregroundPrev = next;
                next.Source.Invalidate();

                foreach (var image in _images)
                {
                    image.Invalidate(next.Source);
                }

                if (_prevPosition?.Position != _nextPosition && PositionChanged != null)
                {
                    _prevPosition ??= new AnimatedImagePositionChangedEventArgs();
                    _prevPosition.Position = _nextPosition;

                    PositionChanged.Invoke(this, _prevPosition);
                }
            }
        }
    }

    public enum AnimatedImageTaskState
    {
        // All good
        None,

        // Buffer was not updated
        Skip,

        // Animation must stop right away
        Stop,

        // A cycle was completed
        Loop,
    }

    public class LottieAnimatedImageTask : AnimatedImageTask
    {
        private readonly LottieAnimation _animation;
        private readonly HashSet<int> _markers;

        public LottieAnimatedImageTask(LottieAnimation animation, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;
            _markers = presentation.Source.Markers?.Values.ToHashSet();

            PixelWidth = presentation.PixelWidth; //animation.PixelWidth;
            PixelHeight = presentation.PixelHeight; //animation.PixelHeight;

            var frameRate = Math.Clamp(animation.FrameRate, 30, presentation.LimitFps ? 30 : 60);
            var interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

            Interval = interval;
        }

        private int _index;

        public override AnimatedImageTaskState NextFrame(PixelBuffer frame, out int position)
        {
            position = 0;

            if (_animation.IsReadyToCache)
            {
                _animation.Cache();
                return AnimatedImageTaskState.Skip;
            }
            else if (_animation.IsCaching)
            {
                return AnimatedImageTaskState.Skip;
            }
            else if (_markers != null && _markers.Contains(_index))
            {
                return AnimatedImageTaskState.Stop;
            }

            var framesPerUpdate = _presentation.LimitFps ? _animation.FrameRate < 60 ? 1 : 2 : 1;

            _animation.RenderSync(frame, _index);
            _index = Math.Min(_animation.TotalFrame, _index + framesPerUpdate);

            if (_animation.TotalFrame == 1)
            {
                _index = 0;
                return AnimatedImageTaskState.Stop;
            }
            else if (_animation.TotalFrame == _index)
            {
                _index = 0;
                return AnimatedImageTaskState.Loop;
            }

            position = _index;
            return AnimatedImageTaskState.None;
        }

        public override void Seek(string marker)
        {
            if (_presentation.Source.Markers.TryGetValue(marker, out int index))
            {
                _index = index + 1;
            }
        }
    }

    public class VideoAnimatedImageTask : AnimatedImageTask
    {
        private readonly CachedVideoAnimation _animation;

        public VideoAnimatedImageTask(CachedVideoAnimation animation, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;

            PixelWidth = animation.PixelWidth;
            PixelHeight = animation.PixelHeight;

            var frameRate = Math.Clamp(animation.FrameRate, 1, 30 /*presentation.LimitFps ? 30 : 60*/);
            var interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

            Interval = interval;
        }

        private int _index;

        public override AnimatedImageTaskState NextFrame(PixelBuffer frame, out int position)
        {
            position = 0;

            if (_animation.IsReadyToCache)
            {
                _animation.Cache();
                return AnimatedImageTaskState.Skip;
            }
            else if (_animation.IsCaching)
            {
                return AnimatedImageTaskState.Skip;
            }

            _animation.RenderSync(frame, out int seconds, out bool completed);
            _index++;

            if (_animation.TotalFrame == 1 || (completed && _index == 1))
            {
                _index = 0;
                return AnimatedImageTaskState.Stop;
            }
            else if (_animation.TotalFrame == _index || completed)
            {
                _index = 0;
                return AnimatedImageTaskState.Loop;
            }

            position = seconds;
            return AnimatedImageTaskState.None;
        }
    }

    public class WebpAnimatedImageTask : AnimatedImageTask
    {
        private readonly IBuffer _animation;

        public WebpAnimatedImageTask(IBuffer animation, Size size, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;

            PixelWidth = (int)size.Width; // animation.PixelWidth;
            PixelHeight = (int)size.Height; // animation.PixelHeight;

            Interval = TimeSpan.FromMilliseconds(1000d / 30);
        }

        public override AnimatedImageTaskState NextFrame(PixelBuffer frame, out int position)
        {
            position = 0;

            BufferSurface.Copy(_animation, frame);
            return AnimatedImageTaskState.Stop;
        }
    }

    public abstract class AnimatedImageTask
    {
        protected readonly AnimatedImagePresentation _presentation;

        protected AnimatedImageTask(AnimatedImagePresentation presentation)
        {
            _presentation = presentation;
        }

        public int PixelWidth { get; init; }
        public int PixelHeight { get; init; }

        public TimeSpan Interval { get; init; }

        public abstract AnimatedImageTaskState NextFrame(PixelBuffer frame, out int position);

        public virtual void Seek(string marker)
        {

        }
    }

    public record AnimatedImagePresentation(AnimatedImageSource Source, int PixelWidth, int PixelHeight, bool LimitFps, int LoopCount, bool AutoPlay, bool IsCachingEnabled);

    public class AnimatedImageLoader
    {
        [ThreadStatic]
        private static AnimatedImageLoader _current;
        public static AnimatedImageLoader Current => _current ??= new();

        private readonly DispatcherQueue _dispatcherQueue;

        private AnimatedImageLoader()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            Debug.Assert(_dispatcherQueue != null);
        }

        public static void Release()
        {
            _current = null;
        }

        private event EventHandler<object> _rendering;
        public event EventHandler<object> Rendering
        {
            add
            {
                if (_rendering == null)
                {
                    Windows.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;
                }

                _rendering += value;
            }
            remove
            {
                _rendering -= value;

                if (_rendering == null)
                {
                    Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
                }
            }
        }

        private void OnRendering(object sender, object e)
        {
            _rendering?.Invoke(sender, e);
        }

        private bool _workStarted;
        private Thread _workThread;

        private readonly WorkQueue _workQueue = new();
        private readonly object _workLock = new();

        private readonly ConcurrentDictionary<int, WeakReference<AnimatedImagePresenter>> _delegates = new();
        private readonly Dictionary<AnimatedImagePresentation, AnimatedImagePresenter> _presenters = new();
        private readonly object _presentersLock = new();

        // Unique per thread
        private int _indexer;

        public AnimatedImagePresenter GetOrCreate(AnimatedImagePresentation configuration)
        {
            lock (_presentersLock)
            {
                if (_presenters.TryGetValue(configuration, out var presenter) && presenter.Increment())
                {
                    return presenter;
                }

                presenter = new AnimatedImagePresenter(this, _dispatcherQueue, configuration);
                _presenters[configuration] = presenter;

                return presenter;
            }
        }

        public void Remove(AnimatedImagePresentation configuration)
        {
            lock (_presentersLock)
            {
                _presenters.Remove(configuration);
            }
        }

        public void Remove(int correlationId)
        {
            _delegates.TryRemove(correlationId, out _);
        }

        public void Load(AnimatedImagePresenter sender)
        {
            if (sender.CorrelationId != 0)
            {
                _delegates.TryRemove(sender.CorrelationId, out _);
            }

            //Logger.Debug();
            var correlationId = ++_indexer;

            sender.CorrelationId = correlationId;

            _delegates[correlationId] = new WeakReference<AnimatedImagePresenter>(sender);
            _workQueue.Push(new WorkItem(correlationId, sender.Presentation));

            lock (_workLock)
            {
                if (_workStarted is false)
                {
                    if (_workThread?.IsAlive is false)
                    {
                        _workThread.Join();
                    }

                    _workStarted = true;
                    _workThread = new Thread(Work);
                    _workThread.Start();
                }
            }
        }

        private void Work()
        {
            while (_workStarted)
            {
                var work = _workQueue.WaitAndPop();
                if (work == null)
                {
                    _workStarted = false;
                    return;
                }

                if (!_delegates.ContainsKey(work.CorrelationId))
                {
                    continue;
                }

                try
                {
                    if (work.Presentation.Source is LocalFileSource local)
                    {
                        var extension = System.IO.Path.GetExtension(local.FilePath);
                        if (string.Equals(extension, ".tgs", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                        {
                            LoadLottie(work, local);
                        }
                        else if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
                        {
                            LoadWebP(work, local);
                        }
                        else
                        {
                            LoadCachedVideo(work);
                        }
                    }
                    else
                    {
                        LoadCachedVideo(work);
                    }
                }
                catch
                {
                    // Shit happens...
                }
            }
        }

        private void LoadLottie(WorkItem work, LocalFileSource local)
        {
            var animation = LottieAnimation.LoadFromFile(local.FilePath, work.Presentation.PixelWidth, work.Presentation.PixelHeight, work.Presentation.IsCachingEnabled, work.Presentation.Source.ColorReplacements, work.Presentation.Source.FitzModifier);
            if (animation != null)
            {
                // TODO: check if animation is valid
                // Width, height, frame rate...

                if (TryGetDelegate(work.CorrelationId, out var target))
                {
                    target.Ready(new LottieAnimatedImageTask(animation, work.Presentation));
                }
                else
                {
                    animation.Dispose();
                }
            }
        }

        private void LoadCachedVideo(WorkItem work)
        {
            var animation = CachedVideoAnimation.LoadFromFile(work.Presentation.Source, work.Presentation.PixelWidth, work.Presentation.PixelHeight, work.Presentation.IsCachingEnabled);
            if (animation != null)
            {
                static bool IsValid(CachedVideoAnimation animation)
                {
                    // TODO: check if animation is valid
                    // Width, height, frame rate...
                    return animation.PixelWidth > 0
                        && animation.PixelHeight > 0
                        && !double.IsNaN(animation.FrameRate);
                }

                if (IsValid(animation) && TryGetDelegate(work.CorrelationId, out var target))
                {
                    target.Ready(new VideoAnimatedImageTask(animation, work.Presentation));
                }
                else
                {
                    animation.Dispose();
                }
            }
        }

        private void LoadWebP(WorkItem work, LocalFileSource local)
        {
            var animation = PlaceholderImageHelper.DrawWebP(local.FilePath, work.Presentation.PixelWidth, out Size size);
            if (animation != null)
            {
                if (TryGetDelegate(work.CorrelationId, out var target))
                {
                    target.Ready(new WebpAnimatedImageTask(animation, size, work.Presentation));
                }
                else
                {
                    //animation.Dispose();
                }
            }
        }

        private bool TryGetDelegate(int correlationId, out AnimatedImagePresenter target)
        {
            if (_delegates.TryRemove(correlationId, out var weak))
            {
                if (weak.TryGetTarget(out target))
                {
                    return true;
                }
            }

            target = null;
            return false;
        }

        record WorkItem(int CorrelationId, AnimatedImagePresentation Presentation);

        class WorkQueue
        {
            private readonly object _workAvailable = new();
            private readonly Stack<WorkItem> _work = new();

            public void Push(WorkItem item)
            {
                lock (_workAvailable)
                {
                    var was_empty = _work.Count == 0;

                    _work.Push(item);

                    if (was_empty)
                    {
                        Monitor.Pulse(_workAvailable);
                    }
                }
            }

            public WorkItem WaitAndPop()
            {
                lock (_workAvailable)
                {
                    while (_work.Count == 0)
                    {
                        var timeout = Monitor.Wait(_workAvailable, 3000);
                        if (timeout is false)
                        {
                            return null;
                        }
                    }

                    return _work.Pop();
                }
            }
        }
    }
}
