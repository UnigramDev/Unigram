//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public partial class AnimatedImagePositionChangedEventArgs : EventArgs
    {
        public double Position { get; set; }
    }

    public partial class AnimatedImageLoopCompletedEventArgs : CancelEventArgs
    {
        public int LoopCount { get; set; }
    }

    public enum AnimatedImageResizeMode
    {
        None,
        Fit,
        Fill
    }

    public partial class AnimatedImage : AnimatedImageBase, IPlayerView
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

        protected bool _clean = false;

        public AnimatedImage()
        {
            DefaultStyleKey = typeof(AnimatedImage);

#if INSTRUMENTATION
            Interlocked.Increment(ref _created);
#endif
        }

#if INSTRUMENTATION
        private static int _created;
        private static int _finalized;

        // See AnimatedImagePresenter's pair: a control that is never finalized is one something is
        // still holding, whether or not this analysis can name what.
        ~AnimatedImage()
        {
            Interlocked.Increment(ref _finalized);
        }

        internal static int DebugFinalized => Volatile.Read(ref _finalized);

        internal static string DebugCounters()
        {
            var created = Volatile.Read(ref _created);
            var finalized = Volatile.Read(ref _finalized);

            return string.Format("  AnimatedImage: created={0}, finalized={1}, alive={2}\n",
                created, finalized, created - finalized);
        }
#endif

        protected override void OnSizeChanged(Size oldSize, Size newSize)
        {
            if (ResizeMode != AnimatedImageResizeMode.None)
            {
                Load();
            }
        }

        public event EventHandler Ready;

        // The presenter reports the position for every frame it shows, and with frames drawn off
        // the UI thread each report is a dispatcher item. Forwarded only while something listens
        // here, which almost no image has.
        private EventHandler<AnimatedImagePositionChangedEventArgs> _positionChanged;

        public event EventHandler<AnimatedImagePositionChangedEventArgs> PositionChanged
        {
            add
            {
                if (_positionChanged == null && _presenter != null)
                {
                    _presenter.PositionChanged += OnPositionChanged;
                }

                _positionChanged += value;
            }
            remove
            {
                _positionChanged -= value;

                if (_positionChanged == null && _presenter != null)
                {
                    _presenter.PositionChanged -= OnPositionChanged;
                }
            }
        }

        public event EventHandler<AnimatedImageLoopCompletedEventArgs> LoopCompleted;
        public event EventHandler Paused;

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

        protected override void OnLoaded()
        {
            // The scale a change made while this was unloaded, taken before the presentation is
            // built from it. The arrange pass carries a change from here on, but this element
            // may not be arranged again before it is drawn.
            if (XamlRoot != null)
            {
                _rasterizationScale = XamlRoot.RasterizationScale;
            }

            Load();

            ReplacementColor?.RegisterColorChangedCallback(OnReplacementColorChanged, ref _replacementColorToken);

            if (Source != null)
            {
                if (IsOutlineEnabled)
                {
                    Source.OutlineChanged += OnOutlineChanged;
                }

                if (IsViewportAware && !_effectiveViewportRegistered)
                {
                    _effectiveViewportRegistered = true;
                    RegisterViewportChanged();
                }
            }
        }

        protected override void OnUnloaded()
        {
            Unload();

            ReplacementColor?.UnregisterColorChangedCallback(ref _replacementColorToken);

            if (Source != null)
            {
                Source.OutlineChanged -= OnOutlineChanged;
            }

            if (_effectiveViewportRegistered)
            {
                _effectiveViewportRegistered = false;
                UnregisterViewportChanged();
            }
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
            if (newValue && IsConnected && Source != null)
            {
                if (!_effectiveViewportRegistered)
                {
                    _effectiveViewportRegistered = true;
                    RegisterViewportChanged();
                }
            }
            else if (_effectiveViewportRegistered)
            {
                _effectiveViewportRegistered = false;
                UnregisterViewportChanged();
            }
        }

        protected override void OnViewportChanged(bool visible)
        {
            if (visible)
            {
                _withinViewport = true;
                Play();

                if (_shimmerPending)
                {
                    UpdateShimmer(Source);
                }
            }
            else
            {
                _withinViewport = false;
                Pause();
            }
        }

        private bool _withinViewport;

        // A shimmer was wanted while the control was off screen. Built when it arrives.
        private bool _shimmerPending;

        // TODO: a bit redunant now as it's already tracked internally
        private bool _effectiveViewportRegistered;

        public void ViewportChanged(bool within)
        {
            if (within && !_withinViewport)
            {
                _withinViewport = true;
                Play();

                if (_shimmerPending)
                {
                    UpdateShimmer(Source);
                }
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                Pause();
            }
        }

        //public bool IsDisabledByPolicy
        //{
        //    get => Type switch
        //    {
        //        AnimatedImageType.Sticker => !PowerSavingPolicy.AutoPlayStickers,
        //        AnimatedImageType.Animation => !PowerSavingPolicy.AutoPlayAnimations,
        //        AnimatedImageType.Emoji => !PowerSavingPolicy.AutoPlayEmoji,
        //        _ => false
        //    };
        //}

        #endregion

        //#region Type

        //public AnimatedImageType Type
        //{
        //    get { return (AnimatedImageType)GetValue(TypeProperty); }
        //    set { SetValue(TypeProperty, value); }
        //}

        //public static readonly DependencyProperty TypeProperty =
        //    DependencyProperty.Register("Type", typeof(AnimatedImageType), typeof(AnimatedImage), new PropertyMetadata(AnimatedImageType.Other));

        //#endregion



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

        private Size _frameSize = new(256, 256);
        public Size FrameSize
        {
            get => _frameSize;
            set
            {
                if (_frameSize != value)
                {
                    _frameSize = value;
                    Load();
                }
            }
        }

        #endregion

        #region DecodeFrameType

        private DecodePixelType _decodeFrameType = DecodePixelType.Physical;
        public DecodePixelType DecodeFrameType
        {
            get => _decodeFrameType;
            set
            {
                if (_decodeFrameType != value)
                {
                    _decodeFrameType = value;
                    Load();
                }
            }
        }

        #endregion

        #region ResizeMode

        private AnimatedImageResizeMode _resizeMode = AnimatedImageResizeMode.None;
        public AnimatedImageResizeMode ResizeMode
        {
            get => _resizeMode;
            set
            {
                if (_resizeMode != value)
                {
                    _resizeMode = value;
                    Load();
                }
            }
        }

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
                var resize = ResizeMode;
                var width = resize != AnimatedImageResizeMode.None ? (int)ActualWidth : (int)FrameSize.Width;
                var height = resize != AnimatedImageResizeMode.None ? (int)ActualHeight : (int)FrameSize.Height;
                var scale = 1d;

                if (DecodeFrameType == DecodePixelType.Logical)
                {
                    width = (int)(width * _rasterizationScale);
                    height = (int)(height * _rasterizationScale);
                    scale = _rasterizationScale;
                }

                if (resize != AnimatedImageResizeMode.None && (width <= 0 || height <= 0))
                {
                    return null;
                }

                return new AnimatedImagePresentation(Source, width, height, scale, LimitFps, LoopCount, AutoPlay, IsCachingEnabled, resize, VisualUtilities.IsInPopupTree(this));
            }

            return null;
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == SourceProperty)
            {
                ((AnimatedImage)d).OnSourceChanged(e);
            }

            ((AnimatedImage)d).Load();
        }

        private void OnSourceChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AnimatedImageSource oldValue)
            {
                oldValue.OutlineChanged -= OnOutlineChanged;
            }

            if (e.NewValue is AnimatedImageSource newValue && IsConnected)
            {
                if (IsOutlineEnabled)
                {
                    newValue.OutlineChanged += OnOutlineChanged;
                }

                if (IsViewportAware && !_effectiveViewportRegistered)
                {
                    _effectiveViewportRegistered = true;
                    RegisterViewportChanged();
                }
            }
        }

        private void OnOutlineChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() => UpdateShimmer(Source));
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
                        _presenter.Unload(this, _state == PlayingState.Playing || _presenter.Presentation.AutoPlay);
                        _presenter.LoopCompleted -= OnLoopCompleted;
                        _presenter.PositionChanged -= OnPositionChanged;
                        _presenter.Paused -= OnPaused;
                        _presenter = null;
                    }

                    _delayedPlay |= _state == PlayingState.Playing;
                    _delayedPlay |= presentation?.AutoPlay ?? false;
                    _state = PlayingState.None;
                    _clean = true;

                    UpdateShimmer(presentation?.Source);

                    if (presentation != null)
                    {
                        _presenter = AnimatedImageLoader.GetOrCreate(XamlRoot, presentation);
                    }

                    if (_presenter != null)
                    {
                        _presenter.LoopCompleted += OnLoopCompleted;
                        _presenter.Paused += OnPaused;

                        if (_positionChanged != null)
                        {
                            _presenter.PositionChanged += OnPositionChanged;
                        }

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
            // TODO: Enable whenever IsDownloadCompleted == false
            if (_clean is false || !IsConnected || !IsOutlineEnabled)
            {
                return;
            }

            // A list realizes well past what it shows, and a placeholder for something nobody is
            // looking at covers nothing. Building it here costs the geometry, a dozen Composition
            // objects and - when the outline is not known yet - a TDLib round trip, per item off
            // screen. Deferred to the moment the control enters the viewport instead.
            //
            // AutoPlay carries the controls no viewport source reports: they show themselves as
            // soon as they load, so there is nothing to wait for.
            //
            // If outline is animated we display the shimmer in any case
            if (!AutoPlay && !_withinViewport && !IsOutlineAnimated)
            {
                _shimmerPending = true;
                return;
            }

            _shimmerPending = false;

            if (source is { Outline.IsReady: true })
            {
                _shimmer = CompositionPathParser.ParseThumbnail(source.Width, source.Height, source.Outline, out ShapeVisual visual, IsOutlineAnimated);
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, visual);
            }
            else
            {
                _shimmer = null;
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, null);

                source?.RequestOutline();
            }
        }

        private void Unload()
        {
            if (_presenter != null && !IsConnected)
            {
                _presenter.Unload(this, _state == PlayingState.Playing);
                _presenter.LoopCompleted -= OnLoopCompleted;
                _presenter.PositionChanged -= OnPositionChanged;
                _presenter.Paused -= OnPaused;
                _presenter = null;

                LayoutRoot.Background = null;
                Show(null);
            }
        }

        private void OnLoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            LoopCompleted?.Invoke(this, e);
        }

        private void OnPositionChanged(object sender, AnimatedImagePositionChangedEventArgs e)
        {
            _positionChanged?.Invoke(this, e);
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _delayedPlay = false;
            _state = PlayingState.Paused;

            Paused?.Invoke(this, e);
        }

        // The frame comes from a composition surface the presenter draws into off the UI thread,
        // painted as this element's background - no child visual, so no composition node of its own.
        // TODO: share these on the presenter, keyed by stretch and tint, as the ImageBrush was before
        // the tint moved into the brush: a XamlCompositionBrushBase can back any number of elements.
        private AnimatedImageSurfaceBrush _surfaceBrush;
        private AnimatedImageSurface _shown;

        // A null surface is the presenter letting go of this image.
        internal void Invalidate(AnimatedImageSurface surface, IBuffer frame, int pixelWidth, int pixelHeight)
        {
            if (IsDisconnected)
            {
                return;
            }

            if (surface == null)
            {
                if (CleanOnSourceChanged)
                {
                    LayoutRoot.Background = null;
                    Show(null);
                }

                return;
            }

            if (!Show(surface))
            {
                return;
            }

            _surfaceBrush ??= new AnimatedImageSurfaceBrush();
            _surfaceBrush.Update(surface.Surface, Stretch);
            _surfaceBrush.TintColor = GetTintColor();

            LayoutRoot.Background = _surfaceBrush;

            if (_clean)
            {
                _clean = false;

                _shimmer = null;
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, null);

                if (DominantColor is SolidColorBrush dominantColor)
                {
                    dominantColor.Color = GetDominantColor(frame, pixelWidth, pixelHeight);
                }

                Ready?.Invoke(this, EventArgs.Empty);
            }
        }

        // Holds a reference on the surface on screen, so that it outlives its presenter for as long
        // as this keeps showing it: with CleanOnSourceChanged off, until the next source draws.
        private bool Show(AnimatedImageSurface surface)
        {
            if (_shown == surface)
            {
                return true;
            }

            if (surface != null && !surface.TryAddRef())
            {
                return false;
            }

            _shown?.Release();
            _shown = surface;
            return true;
        }

        private Color? GetTintColor()
        {
            return ReplacementColor is SolidColorBrush tint && _presenter?.Presentation.Source.NeedsRepainting is true
                ? tint.Color
                : null;
        }

        private unsafe Color GetDominantColor(IBuffer frame, int width, int height)
        {
            if (frame == null || width <= 0 || height <= 0 || frame.Length < width * height * 4)
            {
                return Color.FromArgb(0x55, 0, 0, 0);
            }

            float stepH = (height - 1) / 10f;
            float stepW = (width - 1) / 10f;

            frame.Buffer(out byte* imageBytes);

            int r = 0, g = 0, b = 0;
            int amount = 0;
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    int x = (int)(stepW * i);
                    int y = (int)(stepH * j);
                    int k = (y * width + x) * 4;

                    byte alpha = imageBytes[k + 3];
                    if (alpha > 200)
                    {
                        r += imageBytes[k + 2];
                        g += imageBytes[k + 1];
                        b += imageBytes[k + 0];
                        amount++;
                    }
                }
            }
            if (amount == 0)
            {
                return Color.FromArgb(0x55, 0, 0, 0);
            }

            return Color.FromArgb(255, (byte)(r / amount), (byte)(g / amount), (byte)(b / amount));
        }

        public bool CleanOnSourceChanged { get; set; } = true;

        private Border LayoutRoot;

        protected override void OnApplyTemplate()
        {
            //Logger.Debug();
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Border;

            _templateApplied = true;
            _rasterizationScale = XamlRoot.RasterizationScale;

            Load();
            ReplacementColorChanged();
            base.OnApplyTemplate();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Where a scale change arrives: it makes the core call RecursiveInvalidateMeasure on
            // the visual root, so every element is measured and arranged again. The frames were
            // decoded for the scale, and the ones decoded for the other monitor are the wrong
            // size - so they are decoded again.
            //
            // Not from in here, though. Load reaches Ready through the presenter, and a handler
            // of that is app code that may change the tree - which a layout pass does not
            // survive. Posted instead, which is no later than the callback this replaced.
            if (_templateApplied && DecodeFrameType == DecodePixelType.Logical
                && XamlRoot != null && _rasterizationScale != XamlRoot.RasterizationScale)
            {
                _rasterizationScale = XamlRoot.RasterizationScale;
                this.BeginOnUIThread(Load);
            }

            return base.ArrangeOverride(finalSize);
        }

        #region ReplacementColor

        private bool _needsBrushUpdate;
        private long _replacementColorToken;

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

            if (IsConnected)
            {
                newValue?.RegisterColorChangedCallback(OnReplacementColorChanged, ref _replacementColorToken);
                ReplacementColorChanged();
            }
        }

        private void OnReplacementColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            ReplacementColorChanged();
        }

        protected void ReplacementColorChanged(bool fast = false)
        {
            if (_needsBrushUpdate || (_presenter?.Presentation.Source.NeedsRepainting is not true && _surfaceBrush?.TintColor == null))
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

            if (_surfaceBrush != null)
            {
                _surfaceBrush.TintColor = GetTintColor();
            }
        }

        #endregion

        #region DominantColor

        public SolidColorBrush DominantColor
        {
            get { return (SolidColorBrush)GetValue(DominantColorProperty); }
            set { SetValue(DominantColorProperty, value); }
        }

        public static readonly DependencyProperty DominantColorProperty =
            DependencyProperty.Register("DominantColor", typeof(SolidColorBrush), typeof(AnimatedImage), new PropertyMetadata(null));

        #endregion

        public bool IsOutlineEnabled { get; set; } = true;

        public bool IsOutlineAnimated { get; set; } = false;
    }

    public partial class AnimatedImagePresenter : IAnimation
    {
        private static readonly AnimationScheduler _scheduler = new();
        private static readonly FifoActionWorker _workerQueue = new();

        private readonly AnimatedImagePresentation _presentation;
        private readonly AnimatedImageLoader _loader;

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly List<AnimatedImage> _images = new();

        private volatile int _loaded;
        private volatile int _playing;
        private int _tracker;

        private bool _idle = true;
        private bool _dirty;
        private bool _activated;

        private volatile int _loopCount;

        private int _timerSubscribed;

        private AnimatedImageTask _task;
        private bool _requested;

        private volatile bool _ticking;
        private volatile bool _disposing;
        private volatile bool _disposed;

        // Renders in flight, with the sign bit standing for "closed to new ones". One word rather
        // than two, because taking a slot and finding the gate open has to be a single step: see
        // NextFrame.
        private int _borrows;

        private const int BorrowsClosed = int.MinValue;

        private AnimatedImageLoopCompletedEventArgs _prevCompleted;
        private AnimatedImagePositionChangedEventArgs _prevPosition;
        private double _nextPosition;

        private string _nextMarker;

        public AnimatedImagePresenter(AnimatedImageLoader loader, DispatcherQueue dispatcherQueue, AnimatedImagePresentation configuration)
        {
            _presentation = configuration;
            _loader = loader;

            _dispatcherQueue = dispatcherQueue;
            _tracker++;

#if INSTRUMENTATION
            Interlocked.Increment(ref _created);
#endif
        }

#if INSTRUMENTATION
        private static int _created;
        private static int _finalized;

        // Counted rather than snapshotted, so the answer survives whatever the collector happens to
        // have got to: created minus finalized is what is still alive. The finalizer touches one
        // static int and nothing else, so it cannot resurrect anything or reach a torn-down object.
        ~AnimatedImagePresenter()
        {
            Interlocked.Increment(ref _finalized);
        }

        internal static string DebugCounters()
        {
            var created = Volatile.Read(ref _created);
            var finalized = Volatile.Read(ref _finalized);

            return string.Format("  AnimatedImagePresenter: created={0}, finalized={1}, alive={2}\n",
                created, finalized, created - finalized);
        }
#endif

        public bool Increment()
        {
            if (_tracker > 0)
            {
                _tracker++;
                return true;
            }

            return false;
        }

        public event EventHandler<AnimatedImagePositionChangedEventArgs> PositionChanged;
        public event EventHandler<AnimatedImageLoopCompletedEventArgs> LoopCompleted;

        public event EventHandler Paused;

#if INSTRUMENTATION
        // A presenter is shared and refcounted, so a flat presenter count says nothing about what
        // has accumulated inside one. Every AnimatedImage bound to it subscribes these three and
        // drops them in Unload(), which only runs when IsConnected has gone false - so a handler
        // count that climbs with every panel open names both the leak and the control it holds.
        internal int DebugHandlerCount()
        {
            return (PositionChanged?.GetInvocationList().Length ?? 0)
                + (LoopCompleted?.GetInvocationList().Length ?? 0)
                + (Paused?.GetInvocationList().Length ?? 0);
        }
#endif

        public AnimatedImagePresentation Presentation => _presentation;

        public int CorrelationId { get; set; }

        public void Load(AnimatedImage canvas)
        {
            _images.Add(canvas);
            LoadImpl();

            // The buffer is read only to sample the dominant colour from it, never held: it is
            // recycled between the flusher and the worker queue, which is why Dispose swaps it.
            var task = Volatile.Read(ref _task);
            var surface = Volatile.Read(ref _surface);

            if (_dirty && task != null && surface != null)
            {
                canvas.Invalidate(surface, Volatile.Read(ref _foregroundPrev), task.PixelWidth, task.PixelHeight);
            }
        }

        public void Unload(AnimatedImage canvas, bool playing)
        {
            _images.Remove(canvas);
            UnloadImpl(playing);

            canvas.Invalidate(null, null, 0, 0);
        }

        private void LoadImpl()
        {
            _loaded++;

            if (_loaded == 1 && !_requested)
            {
                _requested = true;

                if (_presentation.Source is DelayedFileSource delayed)
                {
                    // Subscribed before the ask, because the ask answers straight away when the
                    // file is already there.
                    delayed.Downloaded += OnDownloaded;
                    delayed.DownloadFile(DelayedFileDownload.Loaded);
                }
                else
                {
                    _loader.Load(this);
                }
            }
        }

        private void OnDownloaded(object sender, EventArgs e)
        {
            UnsubscribeSource();

            if (_loaded > 0)
            {
                _loader.Load(this);
            }
        }

        /// <summary>
        /// Drops the source subscription. Called from the two teardown points and not from Dispose,
        /// which is only ever reached through one of them - and which two of UnloadImpl's three
        /// teardown branches never reach at all.
        /// </summary>
        private void UnsubscribeSource()
        {
            if (_presentation.Source is DelayedFileSource delayed)
            {
                delayed.Downloaded -= OnDownloaded;
            }
        }

        private void UnloadImpl(bool playing)
        {
            //Logger.Debug();

            _loaded--;
            _tracker--;

            if (playing)
            {
                _playing--;
            }

            if (_loaded <= 0 && _tracker == 0)
            {
                UnsubscribeSource();

                _loader.Activated -= OnActivated;
                _loader.PopupActivated -= OnActivated;
                _loader.Remove(_presentation);

                var task = Volatile.Read(ref _task);
                if (task != null)
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

        public void Play(AnimatedImage canvas)
        {
            PlayImpl();

            // The buffer is read only to sample the dominant colour from it, never held: it is
            // recycled between the flusher and the worker queue, which is why Dispose swaps it.
            var task = Volatile.Read(ref _task);
            var surface = Volatile.Read(ref _surface);

            if (_dirty && task != null && surface != null)
            {
                canvas.Invalidate(surface, Volatile.Read(ref _foregroundPrev), task.PixelWidth, task.PixelHeight);
            }
        }

        public void Pause()
        {
            PauseImpl();
        }

        public void Seek(string marker)
        {
            SeekImpl(marker);
        }

        private void PlayImpl()
        {
            _playing++;
            _idle = false;

            if (_playing == 1 && !_ticking && _loopCount >= 0)
            {
                var task = Volatile.Read(ref _task);
                if (task == null)
                {
                    // Asked again rather than only when the file is still missing: the source
                    // answers an ask it can already satisfy, so a completion that arrived while
                    // nothing was listening is recovered here instead of leaving this with no
                    // branch to take and nothing that would ever come back to it.
                    if (_presentation.Source is DelayedFileSource delayed)
                    {
                        delayed.DownloadFile(DelayedFileDownload.Playing);
                    }
                    else if (!_requested)
                    {
                        _loader.Load(this);
                    }

                    _requested = true;
                    return;
                }

                if (_nextMarker != null)
                {
                    task.Seek(_nextMarker);
                    _nextMarker = null;
                }

                _ticking = _activated;

                if (_ticking)
                {
                    if (Interlocked.CompareExchange(ref _timerSubscribed, 1, 0) == 0)
                    {
                        _scheduler.Subscribe(this);
                    }
                }
                else
                {
                    _workerQueue.Run(RenderNextFrame);
                }
            }
        }

        private void PauseImpl()
        {
            _playing--;
            _idle = false;

            if (_playing == 0)
            {
                _ticking = false;

                var task = Volatile.Read(ref _task);
                if (task == null && _requested)
                {
                    if (_presentation.Source is DelayedFileSource delayed && !delayed.IsDownloadingCompleted)
                    {
                        delayed.DownloadFile(DelayedFileDownload.Unloaded);
                    }
                }
            }
        }

        private void SeekImpl(string marker)
        {
            var pause = _playing > 0;

            _nextMarker = marker;
            Interlocked.Exchange(ref _loopCount, 0);

            if (pause)
            {
                PauseImpl();
            }

            PlayImpl();
        }

        public void Ready(AnimatedImageTask task)
        {
            _dispatcherQueue.TryEnqueue(() => ReadyImpl(task));
        }

        private void ReadyImpl(AnimatedImageTask task)
        {
            if (_loaded > 0)
            {
                Volatile.Write(ref _task, task);
                FrameRate = task.FrameRate;

                CreateResources();

                _ticking = (_idle && _presentation.AutoPlay) || (_playing > 0 && (_activated || _presentation.LoopCount > 0));
                _idle = false;

                if (_ticking)
                {
                    if (Interlocked.CompareExchange(ref _timerSubscribed, 1, 0) == 0)
                    {
                        _scheduler.Subscribe(this);
                    }
                }
                else
                {
                    _workerQueue.Run(RenderNextFrame);
                }
            }
            else if (_tracker == 0)
            {
                UnsubscribeSource();

                _loader.Activated -= OnActivated;
                _loader.PopupActivated -= OnActivated;
                _loader.Remove(_presentation);

                // Ticking should be always false here
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

        #region Resources

        private IBuffer _foregroundPrev;
        private IBuffer _foregroundNext;
        private IBuffer _backgroundNext;

        // The pair the three above pass around, held apart so that both go back to the pool
        // whichever of them each one happens to be in.
        private IBuffer _buffer1;
        private IBuffer _buffer2;

        private AnimatedImageSurface _surface;
        private volatile bool _surfaceShown;
        private int _surfaceUpdatePending;
        private int _surfaceCreatePending;
        private int _redraw;

        // On the flusher's thread, never concurrently with another presenter of the same window:
        // the composition device takes one drawing session at a time, and a second BeginDraw while
        // one is open fails - which lost the only frame a still sticker ever draws.
        // Returns false when the frame has to be drawn again later.
        internal bool FlushFrame()
        {
            var surface = Volatile.Read(ref _surface);
            var next = Interlocked.Exchange(ref _foregroundNext, null);

            if (next == null)
            {
                // A replaced device takes the surface's contents with it, and a still has no next
                // frame that would put them back.
                if (Interlocked.Exchange(ref _redraw, 0) == 1 && surface != null && _foregroundPrev != null && !_disposed)
                {
                    if (!surface.Frame.Draw(_foregroundPrev))
                    {
                        Volatile.Write(ref _redraw, 1);
                        return false;
                    }
                }

                return true;
            }

            Volatile.Write(ref _redraw, 0);

            if (_disposed)
            {
                Interlocked.Exchange(ref _backgroundNext, next);
                return true;
            }

            if (surface == null)
            {
                // Creating it failed, which it does while the device is being replaced. The frame
                // waits for one, unless the worker has already published a newer one.
                if (Interlocked.CompareExchange(ref _foregroundNext, next, null) != null)
                {
                    Interlocked.Exchange(ref _backgroundNext, next);
                }

                if (Interlocked.Exchange(ref _surfaceCreatePending, 1) == 0)
                {
                    _dispatcherQueue.TryEnqueue(CreateSurface);
                }

                return true;
            }

            if (!surface.Frame.Draw(next))
            {
                if (_disposed || Volatile.Read(ref _surface) == null)
                {
                    return true;
                }

                // Put back unless the worker has already published a newer one.
                if (Interlocked.CompareExchange(ref _foregroundNext, next, null) != null)
                {
                    Interlocked.Exchange(ref _backgroundNext, next);
                }

                return false;
            }

            if (_foregroundPrev != null)
            {
                Interlocked.Exchange(ref _backgroundNext, _foregroundPrev);
            }

            _foregroundPrev = next;

            if (!_surfaceShown || PositionChanged != null)
            {
                RequestSurfaceUpdate();
            }

            return true;
        }

        // From the loader, on whichever thread the device was replaced.
        internal void Redraw()
        {
            Volatile.Write(ref _redraw, 1);
            _loader.Flusher.Request(this);
        }

        private void CreateSurface()
        {
            Volatile.Write(ref _surfaceCreatePending, 0);

            var task = Volatile.Read(ref _task);
            if (task == null || _disposed || _surface != null)
            {
                return;
            }

            var frame = _loader.Device.CreateFrameSurface(task.PixelWidth, task.PixelHeight, task.Rotation);
            if (frame != null)
            {
                Volatile.Write(ref _surface, new AnimatedImageSurface(frame));
                _loader.Flusher.Request(this);
            }
        }

        private void RequestSurfaceUpdate()
        {
            if (Interlocked.Exchange(ref _surfaceUpdatePending, 1) == 0)
            {
                _dispatcherQueue.TryEnqueue(SurfaceUpdated);
            }
        }

        private void SurfaceUpdated()
        {
            Volatile.Write(ref _surfaceUpdatePending, 0);

            var surface = Volatile.Read(ref _surface);
            if (surface == null || _disposed)
            {
                return;
            }

            if (_dirty is false)
            {
                var task = Volatile.Read(ref _task);
                var frame = Volatile.Read(ref _foregroundPrev);

                foreach (var image in _images)
                {
                    image.Invalidate(surface, frame, task?.PixelWidth ?? 0, task?.PixelHeight ?? 0);
                }

                _dirty = true;
                _surfaceShown = true;
            }

            if (_prevPosition?.Position != _nextPosition && PositionChanged != null)
            {
                _prevPosition ??= new AnimatedImagePositionChangedEventArgs();
                _prevPosition.Position = _nextPosition;

                PositionChanged.Invoke(this, _prevPosition);
            }
        }

        private readonly SemaphoreSlim _pausedLock = new(0, 1);

        private void CreateResources()
        {
            var task = Volatile.Read(ref _task);
            if (task == null)
            {
                return;
            }

            var width = task.PixelWidth;
            var height = task.PixelHeight;

            _buffer1 = _loader.Buffers.Rent(width, height);
            _buffer2 = _loader.Buffers.Rent(width, height);

            _foregroundPrev = _buffer1;
            _backgroundNext = _buffer2;

            if (_surface == null)
            {
                var frame = _loader.Device.CreateFrameSurface(width, height, task.Rotation);
                if (frame != null)
                {
                    Volatile.Write(ref _surface, new AnimatedImageSurface(frame));
                }
            }

            _activated = _loader.Window.IsActive;

            // Automatically pause only if looping
            if (_presentation.LoopCount != 1)
            {
                _activated = true;
                _loader.Activated += OnActivated;
                _loader.PopupActivated += OnActivated;
            }
        }

        private void InvokePaused()
        {
            Paused?.Invoke(this, EventArgs.Empty);

            _playing = 0;
            _pausedLock.Release();
        }

        private void OnActivated(object sender, PopupActivatedEventArgs args)
        {
            if (_presentation.IsPopup)
            {
                OnActivated(_loader.Window.IsActive);
            }
            else
            {
                OnActivated(_loader.Window.IsActive && !args.IsActive);
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_presentation.IsPopup)
            {
                OnActivated(args.IsActive);
            }
            else
            {
                OnActivated(args.IsActive && !_loader.Window.IsPopupOpened);
            }
        }

        private void OnActivated(bool activated)
        {
            if (_disposed)
            {
                //UnregisterEvents();

                _loader.Activated -= OnActivated;
                _loader.PopupActivated -= OnActivated;
                return;
            }

            Activated(activated);
        }

        public bool Activated(bool active)
        {
            if (_activated != active)
            {
                _activated = active;

                if (_playing > 0 && !active)
                {
                    _ticking = false;
                }
                else if (Volatile.Read(ref _task) != null && _playing > 0 && !_ticking && _loopCount >= 0 && active)
                {
                    _ticking = true;

                    if (Interlocked.CompareExchange(ref _timerSubscribed, 1, 0) == 0)
                    {
                        _scheduler.Subscribe(this);
                    }

                    return true;
                }
            }

            return false;
        }

        #endregion

        public double FrameRate { get; private set; }

        public void RenderNextFrame()
        {
            if (_loaded > 0 && !_disposing && !_disposed)
            {
                NextFrame();
            }

            if (!_ticking)
            {
                //Logger.Debug("-=");
                if (Interlocked.CompareExchange(ref _timerSubscribed, 0, 1) == 1)
                {
                    _scheduler.Unsubscribe(this);
                }

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

                    _loader.Flusher.Request(this);
                }
                else
                {
                    Interlocked.Exchange(ref _backgroundNext, frame);
                }
            }
        }

        /// <summary>
        /// Shuts the gate, and closes the task once nobody is inside a frame. Called by
        /// <see cref="Dispose"/> and by the last borrow to end, whichever happens second, so
        /// exactly one of them does the work.
        /// </summary>
        private void DisposeTask()
        {
            int borrows;

            do
            {
                borrows = Volatile.Read(ref _borrows);

                if (borrows < 0)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _borrows, borrows | BorrowsClosed, borrows) != borrows);

            // A render already inside the decoder cannot be waited for - this runs on the UI thread -
            // so the last one out closes instead. Nothing new can join: the gate is shut above.
            if (borrows == 0)
            {
                CloseTask();
            }
        }

        private void CloseTask()
        {
            Interlocked.Exchange(ref _task, null)?.Dispose();
        }

        private bool NextFrame(IBuffer frame)
        {
            // The borrow. Dispose can run on the UI thread while this is inside the native renderer,
            // and closing the animation underneath it would be a use-after-free. Reading the count
            // and then dropping the task was not enough: a render starting between those two steps
            // was invisible to the close and went on into a decoder that was being freed. Claiming
            // a slot and finding the gate open is one interlocked step, so a close either sees this
            // render or is refused by it, never neither.
            int borrows;

            do
            {
                borrows = Volatile.Read(ref _borrows);

                if (borrows < 0)
                {
                    return false;
                }
            }
            while (Interlocked.CompareExchange(ref _borrows, borrows + 1, borrows) != borrows);

            try
            {
                return NextFrameCore(frame);
            }
            finally
            {
                // Only when the gate shut while this render was running, and this is the last one
                // out: the closer is long gone and left the teardown here.
                if (Interlocked.Decrement(ref _borrows) == BorrowsClosed)
                {
                    CloseTask();
                }
            }
        }

        private bool NextFrameCore(IBuffer frame)
        {
            var task = Volatile.Read(ref _task);
            if (task == null)
            {
                return false;
            }

            // A task that has stopped has no second frame to give - it is a still, and the bitmap
            // it drew is still on screen. PlayImpl checks the same thing before queueing, but it
            // cannot catch this on its own: the stop is only recorded once the render below has
            // run, so two renders queued before the first one executes both pass that check.
            if (_loopCount < 0)
            {
                return false;
            }

            var state = task.NextFrame(frame, out _nextPosition);
            if (state == AnimatedImageTaskState.Stop)
            {
                _ticking = false;
                Interlocked.Exchange(ref _loopCount, -1);
            }
            else if (state == AnimatedImageTaskState.Loop)
            {
                _prevCompleted ??= new AnimatedImageLoopCompletedEventArgs();
                _prevCompleted.Cancel = false;
                _prevCompleted.LoopCount = Interlocked.Increment(ref _loopCount);

                LoopCompleted?.Invoke(this, _prevCompleted);

                if (_prevCompleted.Cancel || (_loopCount >= _presentation.LoopCount && _presentation.LoopCount > 0))
                {
                    _ticking = false;
                    Interlocked.Exchange(ref _loopCount, 0);

                    _dispatcherQueue.TryEnqueue(InvokePaused);
                    _pausedLock.Wait();
                }
            }

            return state != AnimatedImageTaskState.Skip;
        }

        #endregion

        private void Dispose()
        {
            //Logger.Debug();
            //Debug.Assert(_images.Count == 0);

            //_dispatcherQueue.TryEnqueue(UnregisterEvents);

            _disposing = false;
            _disposed = true;

            DisposeTask();

            Interlocked.Exchange(ref _foregroundPrev, null);
            Interlocked.Exchange(ref _foregroundNext, null);
            Interlocked.Exchange(ref _backgroundNext, null);

            ReleaseVisual();

            _loader.Activated -= OnActivated;
            _loader.PopupActivated -= OnActivated;
            _loader.Remove(_presentation);
        }

        /// <summary>
        /// The half of teardown that belongs to the UI thread. Dispose runs on either thread - the
        /// scheduler one whenever UnloadImpl deferred while ticking, which is most of the time - and
        /// the pool lives on the UI thread, so the buffers are posted back to it.
        /// </summary>
        private void ReleaseVisual()
        {
            var first = Interlocked.Exchange(ref _buffer1, null);
            var second = Interlocked.Exchange(ref _buffer2, null);

            Interlocked.Exchange(ref _surface, null)?.Release();
            _surfaceShown = false;

            if (first == null && second == null)
            {
                return;
            }

            var pool = _loader.Buffers;

            if (_dispatcherQueue.HasThreadAccess)
            {
                ReleaseBuffers(pool, first, second);
            }
            else
            {
                // A failed enqueue means the dispatcher is going away, and the pool lives on it -
                // there is nothing left to return them to.
                _dispatcherQueue.TryEnqueue(() => ReleaseBuffers(pool, first, second));
            }
        }

        private static void ReleaseBuffers(AnimatedImageLoader.BufferRecyclePool pool, IBuffer first, IBuffer second)
        {
            pool.Return(first);
            pool.Return(second);
        }
    }

    /// <summary>
    /// The surface a presenter draws into, shared with the images showing it. The last of them to
    /// let go closes it, so an image that keeps its frame across a source change does not go blank
    /// while the next source loads, and nothing waits for the collector to free it.
    /// </summary>
    public sealed partial class AnimatedImageSurface
    {
        private int _references = 1;

        public AnimatedImageSurface(FrameSurface frame)
        {
            Frame = frame;
            Surface = frame.Surface;
        }

        public FrameSurface Frame { get; }

        public CompositionDrawingSurface Surface { get; }

        public bool TryAddRef()
        {
            int references;

            do
            {
                references = Volatile.Read(ref _references);

                if (references == 0)
                {
                    return false;
                }
            }
            while (Interlocked.CompareExchange(ref _references, references + 1, references) != references);

            return true;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                Frame.Dispose();
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

    public partial class LottieAnimatedImageTask : AnimatedImageTask
    {
        private readonly LottieAnimation _animation;
        private readonly bool _shouldStop;

        private readonly HashSet<int> _markers;

        public LottieAnimatedImageTask(LottieAnimation animation, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;
            _shouldStop = !presentation.Source.IsAnimated;

            _markers = presentation.Source.Markers?.Values.ToHashSet();

            PixelWidth = presentation.PixelWidth; //animation.PixelWidth;
            PixelHeight = presentation.PixelHeight; //animation.PixelHeight;

            var frameRate = Math.Clamp(animation.FrameRate, 30, presentation.LimitFps ? 30 : 60);
            var interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

            Interval = interval;
            FrameRate = frameRate;
        }

        private int _index;

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            position = 0;

            // Held, not rendered, while this animation's own cache is building: a cold panel is
            // hundreds of these at once, and rendering them live is the cost the cache exists to
            // avoid. The first frame is already on screen, so it holds rather than blanks.
            if (_animation.IsCaching)
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

            if (_animation.TotalFrame == 1 || _shouldStop)
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

        public override void Dispose()
        {
            _animation.Dispose();
        }
    }

    public partial class VideoAnimatedImageTask : AnimatedImageTask
    {
        private readonly CachedVideoAnimation _animation;
        private readonly bool _shouldStop;

        public VideoAnimatedImageTask(CachedVideoAnimation animation, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;
            _shouldStop = !presentation.Source.IsAnimated;

            PixelWidth = animation.PixelWidth;
            PixelHeight = animation.PixelHeight;
            Rotation = animation.Rotation;

            // Only how often the task is polled: the frames carry their own timestamps and
            // NextFrame skips until the one it holds is due. It must stay constant for the
            // task's lifetime, because AnimationScheduler keys the batch a subscriber is
            // removed from on this value and would otherwise never remove it.
            var frameRate = Math.Clamp(animation.FrameRate, 1, 60 /*presentation.LimitFps ? 30 : 60*/);
            var interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

            Interval = interval;
            FrameRate = frameRate;

            _pollInterval = 1 / frameRate;
            Rewind();
        }

        private int _index;

        private readonly double _pollInterval;

        private long _lastTick;
        private double _clock;
        private double _lastPosition;
        private double _nextDue;

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            position = 0;

            // Still asked, and only here: a video's cache build walks the same decoder this would
            // decode from, so it has to stand aside until the build is done. Lottie has random
            // access and does not.
            if (_animation.IsCaching)
            {
                return AnimatedImageTaskState.Skip;
            }
            else if (!Due())
            {
                return AnimatedImageTaskState.Skip;
            }

            _animation.RenderSync(frame, out double seconds, out bool completed);
            _index++;

            // The next frame's timestamp is only known once it has been decoded, so the one
            // just rendered is held for as long as its predecessor was. The deadline is
            // re-anchored on a real timestamp every time, so the estimate cannot drift.
            // A timestamp that did not move forward means the frame was not rendered at
            // all, and pacing off it would put the deadline in the past and spin.
            _nextDue = seconds > _lastPosition
                ? seconds + (seconds - _lastPosition)
                : seconds + _pollInterval;
            _lastPosition = seconds;

            // completed is authoritative, and TotalFrame deliberately is not: it is 0 until a
            // cache exists, because a video producer cannot know its length in advance. Comparing
            // it against _index used to be a second opinion on where the end is, and the two
            // disagreed the moment a cache was adopted part-way through - TotalFrame jumped to the
            // real count while _index was mid-loop. _index now only distinguishes a video whose
            // first frame is also its last.
            if (_shouldStop || (completed && _index == 1))
            {
                _index = 0;
                Rewind();
                return AnimatedImageTaskState.Stop;
            }
            else if (completed)
            {
                _index = 0;
                Rewind();
                return AnimatedImageTaskState.Loop;
            }

            position = seconds;
            return AnimatedImageTaskState.None;
        }

        private bool Due()
        {
            var now = Stopwatch.GetTimestamp();

            if (_lastTick != 0)
            {
                var elapsed = (now - _lastTick) / (double)Stopwatch.Frequency;

                // A suspended or starved worker comes back owing more time than it can
                // usefully spend: catching up would decode a run of frames nobody sees.
                _clock = elapsed > _pollInterval * 4
                    ? _nextDue
                    : _clock + elapsed;
            }

            _lastTick = now;
            return _clock >= _nextDue;
        }

        private void Rewind()
        {
            _clock = 0;
            _nextDue = 0;

            // One poll behind zero, so the first frame's hold is estimated at the poll
            // interval rather than at nothing, which would show the second frame instantly.
            _lastPosition = -_pollInterval;
        }

        public override void Dispose()
        {
            _animation.Dispose();
        }
    }

    public partial class WebpAnimatedImageTask : AnimatedImageTask
    {
        // Dropped as soon as it has been handed over. A still is decoded once on the loader queue
        // and copied into the presenter's bitmap, which then owns those pixels - holding them here
        // as well is a second copy of every static sticker on screen, and a panel has hundreds.
        private IBuffer _animation;

        public WebpAnimatedImageTask(IBuffer animation, int pixelWidth, int pixelHeight, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;

            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;

            Interval = TimeSpan.FromMilliseconds(1000d / 30);
            FrameRate = 30;
        }

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            position = 0;

            var animation = _animation;
            _animation = null;

            // Stop is returned below, so there is no second frame to serve and nothing to decode
            // it from. Reaching here twice means something drove a stopped task.
            Debug.Assert(animation != null, "WebpAnimatedImageTask rendered twice");

            if (animation != null)
            {
                BufferSurface.Copy(animation, frame);
            }

            return AnimatedImageTaskState.Stop;
        }
    }

    public partial class ParticlesAnimatedImageTask : AnimatedImageTask
    {
        private readonly ParticlesAnimation _animation;

        public ParticlesAnimatedImageTask(ParticlesAnimation animation, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _animation = animation;

            PixelWidth = animation.PixelWidth;
            PixelHeight = animation.PixelHeight;

            Interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            FrameRate = 30;
        }

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            _animation.RenderSync(frame);

            position = 0;
            return AnimatedImageTaskState.None;
        }
    }

    /// <summary>
    /// A dice: up to three lottie layers stacked into one frame, and the switch from the state it
    /// rolls on to the state it lands on.
    /// </summary>
    /// <remarks>
    /// The switch lives here rather than in a change of the control's <see cref="AnimatedImage.Source"/>
    /// because it has to happen on a loop boundary and not before the next animation is loaded -
    /// two moments, on two threads. Replacing the source would tear the presenter down between
    /// them and blank the message; swapping a field inside the running task cannot.
    /// </remarks>
    public partial class DiceAnimatedImageTask : AnimatedImageTask
    {
        private readonly DiceFileSource _source;

        // Slot machine only. The chrome is a still, so it is rendered once and copied under every
        // frame rather than re-rendered as one - the cheapest layer should not be the dearest.
        private readonly IBuffer _backdrop;
        private readonly LottieAnimation _lever;

        private LottieAnimation _reels;

        // The final state, loaded while the initial one is still looping. Published by the thread
        // that built it and taken by the render thread at a loop boundary, the one point where
        // exchanging it does not show.
        private LottieAnimation _next;
        private int _preparing;
        private volatile bool _disposed;

        private int _index;
        private int _leverIndex;

        // The initial state loops for as long as the message takes to send; the final one plays
        // once and is done.
        private bool _looping;
        private bool _completed;

        public DiceAnimatedImageTask(DiceFileSource source, LottieAnimation reels, IBuffer backdrop, LottieAnimation lever, bool looping, AnimatedImagePresentation presentation)
            : base(presentation)
        {
            _source = source;
            _reels = reels;
            _backdrop = backdrop;
            _lever = lever;
            _looping = looping;

            // A result that has already been seen opens on its last frame. Re-rolling it every time
            // the message scrolls back into view would be a lie about when it happened.
            _index = looping || source.IsContentUnread ? 0 : reels.TotalFrame - 1;

            PixelWidth = presentation.PixelWidth;
            PixelHeight = presentation.PixelHeight;

            var frameRate = Math.Clamp(reels.FrameRate, 30, presentation.LimitFps ? 30 : 60);

            Interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));
            FrameRate = frameRate;
        }

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            position = 0;

            var reels = _reels;
            if (reels == null)
            {
                return AnimatedImageTaskState.Skip;
            }

            // The result is taken at frame zero, which is the loop boundary and also the state a
            // task that has not played yet is in. One rule covers both: a dice whose result arrives
            // while it is still rolling waits for the roll to come round, and one whose result
            // arrives before anything started moves to it now rather than rolling once for nothing.
            if (_looping)
            {
                LottieAnimation next = null;

                if (_index == 0)
                {
                    next = Interlocked.Exchange(ref _next, null);
                }

                if (next != null)
                {
                    reels.Dispose();

                    reels = _reels = next;
                    _looping = false;
                }
                else
                {
                    // Asked on every frame, and not only on the boundary the swap happens on. The
                    // roll is a placeholder for a result that is still coming, so the load has to
                    // start the moment it can: asking once a turn spent one whole turn noticing the
                    // result and another loading it, and swapped on the third.
                    Prepare();
                }
            }

            // Bottom to top into the one buffer: the first layer replaces what the buffer held, and
            // everything above it composites over what is already there.
            if (_backdrop != null)
            {
                BufferSurface.Copy(_backdrop, frame);
                reels.RenderSync(frame, _index, false);
            }
            else
            {
                reels.RenderSync(frame, _index, true);
            }

            if (_lever != null)
            {
                _lever.RenderSync(frame, _leverIndex, false);
            }

            var framesPerUpdate = _presentation.LimitFps ? reels.FrameRate < 60 ? 1 : 2 : 1;

            // Pulled once, and left down for every roll after it.
            if (_lever != null && _leverIndex + framesPerUpdate < _lever.TotalFrame)
            {
                _leverIndex += framesPerUpdate;
            }

            // Only the final state reports its frames. A caller watching for one particular frame
            // of the result - the point a win throws confetti - would otherwise see the initial
            // state come round on it again and again.
            position = _looping ? 0 : _index;

            if (_index + framesPerUpdate < reels.TotalFrame)
            {
                _index += framesPerUpdate;
                return AnimatedImageTaskState.None;
            }

            if (!_looping)
            {
                // Loop once to say the result has played out, and Stop on the tick after it so that
                // nothing drives this again. Two ticks rather than one because Stop hands its
                // buffer forward like any other frame, and a buffer nothing wrote into holds the
                // frame before last. The index is left at the end, so both draw the result.
                if (_completed)
                {
                    return AnimatedImageTaskState.Stop;
                }

                _completed = true;
                return AnimatedImageTaskState.Loop;
            }

            // Round again, and the top of the next frame is where the result is taken up if it has
            // arrived. Not Loop: that is what tells the presenter the result has played out, and
            // what stops it - a roll coming round has done neither.
            _index = 0;

            return AnimatedImageTaskState.None;
        }

        /// <summary>
        /// Starts loading the final state, if the server has named it and its files have arrived.
        /// Asked on every frame of the initial state, which is what that state is waiting for.
        /// </summary>
        /// <remarks>
        /// Ordered so that the frames with nothing to do - every frame until the result lands - cost
        /// a volatile read and a handful of field reads, and allocate nothing. The interlocked gate
        /// is reached only by the one frame that starts the load, and is never given back: a result
        /// is loaded once, and a file that would not parse will not parse on the next frame either.
        /// </remarks>
        private void Prepare()
        {
            if (Volatile.Read(ref _preparing) != 0)
            {
                return;
            }

            var state = _source.FinalState;
            if (state == null || !state.IsDownloadingCompleted())
            {
                return;
            }

            if (Interlocked.Exchange(ref _preparing, 1) != 0)
            {
                return;
            }

            var width = _presentation.PixelWidth;
            var height = _presentation.PixelHeight;

            // Off this thread: merging three reels is a gzip and three JSON documents, and the
            // scheduler that calls NextFrame is shared with every other animation on screen.
            Task.Run(() =>
            {
                var animation = DiceFileSource.CreateReels(state, width, height);
                if (animation is not { TotalFrame: > 0 })
                {
                    // Nothing to swap to. The initial state carries on looping, which is a better
                    // answer than a frame of whatever the buffer happened to hold.
                    animation?.Dispose();
                    return;
                }

                Interlocked.Exchange(ref _next, animation)?.Dispose();

                // Disposed while this was loading: nothing will ever come to take it, and it holds
                // a parsed document and its buffers until something does.
                if (_disposed)
                {
                    Interlocked.Exchange(ref _next, null)?.Dispose();
                }
            });
        }

        public override void Dispose()
        {
            _disposed = true;

            // No lock against NextFrame: the presenter holds this off until no render is in flight,
            // which is the same guarantee every other task relies on.
            _reels?.Dispose();
            _lever?.Dispose();

            Interlocked.Exchange(ref _next, null)?.Dispose();
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

        public int Rotation { get; init; }

        public TimeSpan Interval { get; init; }

        public double FrameRate { get; init; }

        public abstract AnimatedImageTaskState NextFrame(IBuffer frame, out double position);

        /// <summary>
        /// Closes the native animation. Dropping the reference is not enough: it holds a decoder,
        /// a cache file handle and its buffers, and nothing else releases them deterministically.
        /// </summary>
        public virtual void Dispose()
        {

        }

        public virtual void Seek(string marker)
        {

        }
    }

    public record AnimatedImagePresentation(AnimatedImageSource Source, int PixelWidth, int PixelHeight, double RasterizationScale, bool LimitFps, int LoopCount, bool AutoPlay, bool IsCachingEnabled, AnimatedImageResizeMode ResizeMode, bool IsPopup);

    /// <summary>
    /// Paints a presenter's composition surface as a XAML brush, optionally recoloured. The tint is
    /// an effect over the surface itself: a colour matrix that keeps only the alpha, then a tint.
    /// </summary>
    public sealed partial class AnimatedImageSurfaceBrush : XamlCompositionBrushBase
    {
        private CompositionDrawingSurface _surface;
        private CompositionStretch _stretch = CompositionStretch.Uniform;
        private Color? _tintColor;

        private CompositionSurfaceBrush _surfaceBrush;
        private CompositionEffectBrush _effectBrush;
        private bool _effectOpaque;

        // Per view thread, like the compositor they belong to.
        [ThreadStatic]
        private static CompositionEffectFactory _opaqueFactory;
        [ThreadStatic]
        private static CompositionEffectFactory _identityFactory;

        public void Update(CompositionDrawingSurface surface, Stretch stretch)
        {
            _surface = surface;
            _stretch = stretch switch
            {
                Stretch.None => CompositionStretch.None,
                Stretch.Fill => CompositionStretch.Fill,
                Stretch.UniformToFill => CompositionStretch.UniformToFill,
                _ => CompositionStretch.Uniform
            };

            if (_surfaceBrush != null)
            {
                _surfaceBrush.Surface = surface;
                _surfaceBrush.Stretch = _stretch;
            }
        }

        public Color? TintColor
        {
            get => _tintColor;
            set
            {
                if (_tintColor == value)
                {
                    return;
                }

                _tintColor = value;

                if (_surfaceBrush != null)
                {
                    Rebuild();
                }
            }
        }

        protected override void OnConnected()
        {
            Rebuild();
        }

        protected override void OnDisconnected()
        {
            CompositionBrush = null;

            _effectBrush?.Dispose();
            _effectBrush = null;

            _surfaceBrush?.Dispose();
            _surfaceBrush = null;
        }

        private void Rebuild()
        {
            if (_surface == null)
            {
                return;
            }

            var compositor = _surface.Compositor;

            if (_surfaceBrush == null)
            {
                _surfaceBrush = compositor.CreateSurfaceBrush(_surface);
                _surfaceBrush.Stretch = _stretch;
            }

            if (_tintColor is not Color tint)
            {
                _effectBrush?.Dispose();
                _effectBrush = null;

                CompositionBrush = _surfaceBrush;
                return;
            }

            // A transparent replacement keeps the original colours, as in UpdateBrush.
            var opaque = tint.A != 0;

            if (_effectBrush == null || _effectOpaque != opaque)
            {
                _effectBrush?.Dispose();
                _effectBrush = GetFactory(compositor, opaque).CreateBrush();
                _effectBrush.SetSourceParameter("Source", _surfaceBrush);
                _effectOpaque = opaque;
            }

            _effectBrush.Properties.InsertColor("Tint.Color", tint);
            CompositionBrush = _effectBrush;
        }

        private static CompositionEffectFactory GetFactory(Compositor compositor, bool opaque)
        {
            var factory = opaque ? _opaqueFactory : _identityFactory;

            if (factory == null)
            {
                var colorMatrix = new Matrix5x4();

                if (opaque)
                {
                    colorMatrix.M51 = colorMatrix.M52 = colorMatrix.M53 = colorMatrix.M44 = 1;
                }
                else
                {
                    colorMatrix.M11 = colorMatrix.M22 = colorMatrix.M33 = colorMatrix.M44 = 1;
                }

                var effect = new TintEffect
                {
                    Name = "Tint",
                    Source = new ColorMatrixEffect
                    {
                        Source = new CompositionEffectSourceParameter("Source"),
                        ColorMatrix = colorMatrix
                    }
                };

                factory = compositor.CreateEffectFactory(effect, new[] { "Tint.Color" });

                if (opaque)
                {
                    _opaqueFactory = factory;
                }
                else
                {
                    _identityFactory = factory;
                }
            }

            return factory;
        }
    }

    public partial class AnimatedImageLoader
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WindowContext _window;
        private readonly CompositionGraphicsDevice _graphicsDevice;

        public WindowContext Window => _window;

        public Direct2DDevice Device { get; }

        private AnimatedImageLoader(WindowContext window)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _window = window;

            // Taken here because Direct2D.Current is per view thread, and frames are drawn on the
            // flusher's, which has none.
            Device = Direct2D.Current;
            Flusher = new FrameFlusher(Device);

            _graphicsDevice = Device.Device;
            _graphicsDevice.RenderingDeviceReplaced += OnRenderingDeviceReplaced;

            Debug.Assert(_dispatcherQueue != null);
        }

        public FrameFlusher Flusher { get; }

        private void OnRenderingDeviceReplaced(CompositionGraphicsDevice sender, RenderingDeviceReplacedEventArgs args)
        {
            // Rare enough that a copy is the simplest way not to call out under the lock.
            AnimatedImagePresenter[] presenters;

            lock (_presentersLock)
            {
                presenters = _presenters.Values.ToArray();
            }

            foreach (var presenter in presenters)
            {
                presenter.Redraw();
            }
        }

        /// <summary>
        /// Draws every presenter's pending frame into its surface, one after another, on one thread
        /// per window. The composition device allows a single drawing session at a time, so workers
        /// drawing their own frames failed whenever two overlapped, and queued on the device lock
        /// ahead of XAML's own commit when they did not.
        /// </summary>
        public sealed class FrameFlusher
        {
            private readonly object _lock = new();
            private readonly HashSet<AnimatedImagePresenter> _pending = new();
            private readonly AutoResetEvent _signal = new(false);
            private readonly Direct2DDevice _device;
            private Thread _thread;
            private volatile bool _closed;

            public FrameFlusher(Direct2DDevice device)
            {
                _device = device;
            }

            public void Request(AnimatedImagePresenter presenter)
            {
                lock (_lock)
                {
                    if (_closed || !_pending.Add(presenter))
                    {
                        return;
                    }

                    if (_thread == null)
                    {
                        _thread = new Thread(Run) { IsBackground = true, Name = "AnimatedImage flush" };
                        _thread.Start();
                    }
                }

                _signal.Set();
            }

            public void Close()
            {
                _closed = true;
                _signal.Set();
            }

            private void Run()
            {
                var batch = new List<AnimatedImagePresenter>();
                var retry = new List<AnimatedImagePresenter>();

                while (!_closed)
                {
                    _signal.WaitOne();

                    // One pass per compositor frame: every pass is a commit, and every commit a XAML
                    // frame on the UI thread, so frames decoded meanwhile have to share it.
                    _device.WaitForCompositorClock();

                    lock (_lock)
                    {
                        batch.AddRange(_pending);
                        _pending.Clear();
                    }

                    foreach (var presenter in batch)
                    {
                        if (!presenter.FlushFrame())
                        {
                            retry.Add(presenter);
                        }
                    }

                    batch.Clear();

                    if (retry.Count > 0)
                    {
                        // A draw fails while the device is being replaced; give it a frame's time.
                        Thread.Sleep(16);

                        lock (_lock)
                        {
                            foreach (var presenter in retry)
                            {
                                _pending.Add(presenter);
                            }
                        }

                        retry.Clear();
                        _signal.Set();
                    }
                }
            }
        }

        public static void Release(XamlRoot xamlRoot)
        {
            if (xamlRoot != null && _loaders.TryGetValue(xamlRoot, out var loader))
            {
                loader.ReleaseImpl(xamlRoot);
            }
        }

        private void ReleaseImpl(XamlRoot xamlRoot)
        {
            Flusher.Close();
            _graphicsDevice.RenderingDeviceReplaced -= OnRenderingDeviceReplaced;

            if (_window.XamlRoot != null)
            {
                _loaders.Remove(_window.XamlRoot);
            }

            Buffers.Clear();
        }

        /// <summary>The frame buffers this window's presenters render into.</summary>
        public BufferRecyclePool Buffers { get; } = new();

        /// <summary>
        /// Recycles the pair of buffers every presenter renders into, so a panel scroll reuses a
        /// bounded set instead of allocating two per sticker.
        /// </summary>
        /// <remarks>
        /// One per <see cref="XamlRoot"/>, because it lives on the loader. That makes every member
        /// single-threaded - Rent comes from ReadyImpl and Return from
        /// AnimatedImagePresenter.ReleaseVisual, both on this loader's dispatcher - which is why
        /// nothing here locks. Anything that starts calling it from elsewhere has to revisit that.
        /// </remarks>
        public sealed class BufferRecyclePool
        {
            // Long enough to survive a scroll that turns straight back, short enough that a panel
            // the user has left does not sit on its buffers.
            private const ulong Expiration = 5000;

            // A frame only needs its own bytes from the start of the buffer, so any buffer at
            // least that long will do - but the presenter keeps it for as long as it lives, and a
            // panel sticker's would otherwise end up behind every custom emoji after it.
            private const uint MaxWaste = 2;

            // Few enough, a panel's worth at most, that a scan is cheaper than keeping them sorted.
            private readonly List<Entry> _buffers = new();

            private DispatcherTimer _timer;

            private bool _closed;

            private readonly record struct Entry(IBuffer Buffer, uint Length, ulong Expires);

            public IBuffer Rent(int width, int height)
            {
                var size = (uint)(width * height * 4);
                var best = -1;

                // From the end and keeping the first of equals: the newest is the one most likely
                // still in cache.
                for (int i = _buffers.Count - 1; i >= 0; i--)
                {
                    var length = _buffers[i].Length;

                    if (length >= size && length / MaxWaste <= size && (best == -1 || length < _buffers[best].Length))
                    {
                        best = i;

                        if (length == size)
                        {
                            break;
                        }
                    }
                }

                if (best != -1)
                {
                    var buffer = _buffers[best].Buffer;
                    _buffers.RemoveAt(best);

                    // Not cleared: the caller renders a whole frame into it before it is shown, so
                    // clearing would be a memset per realization for nothing.
                    return buffer;
                }

                return BufferSurface.Create(size);
            }

            public void Return(IBuffer buffer)
            {
                if (buffer == null || _closed)
                {
                    return;
                }

                _buffers.Add(new Entry(buffer, buffer.Length, Logger.TickCount + Expiration));

                _timer ??= CreateTimer();

                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                }
            }

            /// <summary>Drops everything at once, for a window that is going away.</summary>
            public void Clear()
            {
                _buffers.Clear();
                _timer?.Stop();
            }

            private DispatcherTimer CreateTimer()
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };

                timer.Tick += OnTick;
                return timer;
            }

            private void OnTick(object sender, object e)
            {
                var now = Logger.TickCount;

                for (int i = _buffers.Count - 1; i >= 0; i--)
                {
                    if (now > _buffers[i].Expires)
                    {
                        _buffers.RemoveAt(i);
                    }
                }

                if (_buffers.Count == 0)
                {
                    _timer.Stop();
                }
            }
        }

        public event EventHandler<WindowActivatedEventArgs> Activated
        {
            add => _window.Activated += value;
            remove => _window.Activated -= value;
        }

        public event EventHandler<PopupActivatedEventArgs> PopupActivated
        {
            add => _window.PopupActivated += value;
            remove => _window.PopupActivated -= value;
        }

        private readonly ParallelActionWorker _workQueue = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 4));

        private readonly ConcurrentDictionary<int, WeakReference<AnimatedImagePresenter>> _delegates = new();
        private readonly Dictionary<AnimatedImagePresentation, AnimatedImagePresenter> _presenters = new();
        private readonly object _presentersLock = new();

        // Unique per thread
        private int _indexer;

        private static readonly ConditionalWeakTable<XamlRoot, AnimatedImageLoader> _loaders = new();

        public static AnimatedImagePresenter GetOrCreate(XamlRoot xamlRoot, AnimatedImagePresentation configuration)
        {
            Debug.Assert(xamlRoot != null);

            var window = WindowContext.ForXamlRoot(xamlRoot);
            if (window != null)
            {
                var loader = _loaders.GetOrAdd(xamlRoot, x => new AnimatedImageLoader(window));
                return loader.GetOrCreate(configuration);
            }

            return null;
        }

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

#if INSTRUMENTATION
        // Counts, not roots. _presenters is a strong table that lives as long as the window, keyed
        // by a record whose equality runs through AnimatedImageSource.Equals - so a count that
        // climbs with every panel open and never comes back down is the leak itself, whatever it
        // turns out to be holding at the other end.
        public static string DebugReport()
        {
            var report = AnimatedImage.DebugCounters() + AnimatedImagePresenter.DebugCounters();

            foreach (var pair in _loaders)
            {
                var loader = pair.Value;

                lock (loader._presentersLock)
                {
                    var handlers = 0;
                    var worst = 0;

                    foreach (var presenter in loader._presenters.Values)
                    {
                        var count = presenter.DebugHandlerCount();

                        handlers += count;
                        worst = Math.Max(worst, count);
                    }

                    report += string.Format("  AnimatedImageLoader: presenters={0}, queued={1}, handlers={2} (worst {3})\n",
                        loader._presenters.Count, loader._delegates.Count, handlers, worst);
                }
            }

            return report;
        }
#endif

        public void Load(AnimatedImagePresenter sender)
        {
            if (sender.CorrelationId != 0 && _delegates.ContainsKey(sender.CorrelationId))
            {
                // Already queued, don't enqueue again
                return;
            }

            var correlationId = ++_indexer;

            sender.CorrelationId = correlationId;

            _delegates[correlationId] = new WeakReference<AnimatedImagePresenter>(sender);

            // Hoisted: capturing sender would root the presenter for as long as the item sits in
            // the queue, which is exactly what the WeakReference above exists to avoid - and the
            // queue is longest when a cache-building scroll is under way.
            var presentation = sender.Presentation;
            _workQueue.Run(() => Work(new WorkItem(correlationId, presentation)));
        }

        private void Work(WorkItem work)
        {
            if (!_delegates.TryRemove(work.CorrelationId, out var weakDelegate))
            {
                return;
            }

            try
            {
                if (work.Presentation.Source is DiceFileSource dice)
                {
                    LoadDice(weakDelegate, work, dice);
                }
                else if (work.Presentation.Source is LocalFileSource local)
                {
                    if (local.Format is StickerFormatTgs)
                    {
                        LoadLottie(weakDelegate, work, local);
                    }
                    else if (local.Format is StickerFormatWebp)
                    {
                        LoadWebP(weakDelegate, work, local);
                    }
                    else if (local.Format is StickerFormatWebm)
                    {
                        LoadCachedVideo(weakDelegate, work);
                    }
                    else
                    {
                        if (local.FilePath.HasExtension(".tgs", ".json"))
                        {
                            LoadLottie(weakDelegate, work, local);
                        }
                        else if (local.FilePath.HasExtension(".webp"))
                        {
                            LoadWebP(weakDelegate, work, local);
                        }
                        else
                        {
                            LoadCachedVideo(weakDelegate, work);
                        }
                    }
                }
                else if (work.Presentation.Source is ParticlesImageSource particles)
                {
                    LoadParticles(weakDelegate, work, particles);
                }
                else
                {
                    LoadCachedVideo(weakDelegate, work);
                }
            }
            catch
            {
                // Shit happens...
                NotifyDelegate(weakDelegate, null, null);
            }
        }

        private void LoadDice(WeakReference<AnimatedImagePresenter> weakDelegate, WorkItem work, DiceFileSource dice)
        {
            var width = work.Presentation.PixelWidth;
            var height = work.Presentation.PixelHeight;

            var state = dice.StartState;

            // TotalFrame and not null: LoadFromFile hands back an animation before it has parsed
            // anything, so this is the first point at which the file is known to be renderable. The
            // reels draw the whole frame, and a refused render would leave the recycled bitmap
            // showing whatever the last animation to use it drew.
            var reels = DiceFileSource.CreateReels(state, width, height);
            if (reels is not { TotalFrame: > 0 })
            {
                reels?.Dispose();

                NotifyDelegate(weakDelegate, null, null);
                return;
            }

            IBuffer backdrop = null;

            using (var background = DiceFileSource.CreateBackground(state, width, height))
            {
                // TotalFrame parses the file, so it is also the answer to whether there is one to
                // draw: it comes back 0 for anything the renderer refused.
                var total = background?.TotalFrame ?? 0;

                if (total > 0)
                {
                    // The chrome is a still, drawn once and copied under every frame of the reels
                    // rather than re-rendered as one. Frame 1 where there is one, because that is
                    // what this has always drawn.
                    //
                    // Zeroed, and not BufferSurface.Create(size), which mallocs: RenderSync says
                    // nothing about whether it wrote anything, and an out-of-range frame or a file
                    // it cannot parse leaves the buffer exactly as it found it. Uninitialised, that
                    // is a solid block of heap under the whole message.
                    backdrop = BufferSurface.Create(new byte[width * height * 4]);
                    background.RenderSync(backdrop, Math.Min(1, total - 1));
                }
            }

            var lever = DiceFileSource.CreateLever(state, width, height);

            if (lever is not { TotalFrame: > 0 })
            {
                lever?.Dispose();
                lever = null;
            }

            // Disposed by hand rather than through NotifyDelegate's disposable, which takes one
            // object: a dice holds up to three animations, and only the task knows about all of
            // them.
            var task = new DiceAnimatedImageTask(dice, reels, backdrop, lever, dice.IsLooping, work.Presentation);

            if (!NotifyDelegate(weakDelegate, null, task))
            {
                task.Dispose();
            }
        }

        private void LoadParticles(WeakReference<AnimatedImagePresenter> weakDelegate, WorkItem work, ParticlesImageSource particles)
        {
            var animation = new ParticlesAnimation(work.Presentation.PixelWidth, work.Presentation.PixelHeight, work.Presentation.RasterizationScale, particles.Type, particles.Foreground, particles.Background);
            NotifyDelegate(weakDelegate, null, new ParticlesAnimatedImageTask(animation, work.Presentation));
        }

        private void LoadLottie(WeakReference<AnimatedImagePresenter> weakDelegate, WorkItem work, LocalFileSource local)
        {
            static bool IsValid(AnimatedImagePresentation presentation)
            {
                // TODO: check if animation is valid
                // Width, height, frame rate...
                return presentation.PixelWidth > 0
                    && presentation.PixelHeight > 0;
            }

            var animation = LottieAnimation.LoadFromFile(local.FilePath, work.Presentation.PixelWidth, work.Presentation.PixelHeight, work.Presentation.IsCachingEnabled, work.Presentation.Source.ColorReplacements, work.Presentation.Source.FitzModifier);
            if (animation != null)
            {
                if (IsValid(work.Presentation))
                {
                    NotifyDelegate(weakDelegate, animation, new LottieAnimatedImageTask(animation, work.Presentation));
                }
                else
                {
                    animation.Dispose();
                }
            }
        }

        private void LoadCachedVideo(WeakReference<AnimatedImagePresenter> weakDelegate, WorkItem work)
        {
            static bool IsValid(CachedVideoAnimation animation)
            {
                // TODO: check if animation is valid
                // Width, height, frame rate...
                return animation.PixelWidth > 0
                    && animation.PixelHeight > 0
                    && !double.IsNaN(animation.FrameRate);
            }

            var animation = CachedVideoAnimation.LoadFromFile(work.Presentation.Source, work.Presentation.PixelWidth, work.Presentation.PixelHeight, work.Presentation.ResizeMode == AnimatedImageResizeMode.Fit, work.Presentation.IsCachingEnabled, work.Presentation.LimitFps);
            if (animation != null)
            {
                if (IsValid(animation))
                {
                    if (work.Presentation.Source.SeekToSeconds != 0)
                    {
                        animation.Seek(work.Presentation.Source.SeekToSeconds);
                    }

                    NotifyDelegate(weakDelegate, animation, new VideoAnimatedImageTask(animation, work.Presentation));
                }
                else
                {
                    animation.Dispose();
                }
            }
        }

        private async void LoadWebP(WeakReference<AnimatedImagePresenter> weakDelegate, WorkItem work, LocalFileSource local)
        {
            static bool IsValid(IBuffer animation, int pixelWidth, int pixelHeight)
            {
                // TODO: check if animation is valid
                // Width, height, frame rate...
                return pixelWidth > 0
                    && pixelHeight > 0
                    && animation.Length == pixelWidth * pixelHeight * 4;
            }

            var animation = Direct2DDevice.DrawWebP(local.FilePath, work.Presentation.PixelWidth, out int pixelWidth, out int pixelHeight);
            if (animation != null)
            {
                if (IsValid(animation, pixelWidth, pixelHeight))
                {
                    NotifyDelegate(weakDelegate, null, new WebpAnimatedImageTask(animation, pixelWidth, pixelHeight, work.Presentation));
                }
            }
            else
            {
                try
                {
                    // If the image fails to decode as WebP, we try to decode it again using system image decoders.
                    var file = await StorageFile.GetFileFromPathAsync(local.FilePath);

                    using var stream = await file.OpenReadAsync();
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var transform = new BitmapTransform();

                    if (decoder.PixelWidth > work.Presentation.PixelWidth || decoder.PixelHeight > work.Presentation.PixelWidth)
                    {
                        var ratioX = (double)work.Presentation.PixelWidth / decoder.PixelWidth;
                        var ratioY = (double)work.Presentation.PixelWidth / decoder.PixelHeight;
                        var ratio = Math.Min(ratioX, ratioY);

                        transform.ScaledWidth = (uint)(decoder.PixelWidth * ratio);
                        transform.ScaledHeight = (uint)(decoder.PixelHeight * ratio);

                        pixelWidth = (int)transform.ScaledWidth;
                        pixelHeight = (int)transform.ScaledHeight;
                    }
                    else
                    {
                        pixelWidth = (int)decoder.PixelWidth;
                        pixelHeight = (int)decoder.PixelHeight;
                    }

                    var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                    var bytes = pixels.DetachPixelData();

                    animation = BufferSurface.Create(bytes);

                    if (IsValid(animation, pixelWidth, pixelHeight))
                    {
                        NotifyDelegate(weakDelegate, null, new WebpAnimatedImageTask(animation, pixelWidth, pixelHeight, work.Presentation));
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                    NotifyDelegate(weakDelegate, null, null);
                }
            }
        }

        private bool NotifyDelegate(WeakReference<AnimatedImagePresenter> weakDelegate, IDisposable disposable, AnimatedImageTask task)
        {
            static bool IsValid(AnimatedImageTask task)
            {
                // TODO: check if animation is valid
                // Width, height, frame rate...
                return task != null
                    && task.PixelWidth > 0
                    && task.PixelHeight > 0;
            }

            if (TryGetDelegate(weakDelegate, out var target) && IsValid(task))
            {
                target.Ready(task);
                return true;
            }

            disposable?.Dispose();
            return false;
        }

        private bool TryGetDelegate(WeakReference<AnimatedImagePresenter> weakDelegate, out AnimatedImagePresenter target)
        {
            if (weakDelegate.TryGetTarget(out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        record WorkItem(int CorrelationId, AnimatedImagePresentation Presentation);
    }
}
