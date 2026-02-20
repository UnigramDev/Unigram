//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
using Windows.UI.Core;
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
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            if (ResizeMode != AnimatedImageResizeMode.None)
            {
                Load();
            }

            UpdateRotation(LayoutRoot.Background as ImageBrush);
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

        protected override void OnLoaded()
        {
            Load();

            base.OnLoaded();
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

            base.OnUnloaded();
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
                Play();
            }
            else
            {
                Pause();
            }
        }

        private bool _withinViewport;
        private bool _visible = true;

        // TODO: a bit redunant now as it's already tracked internally
        private bool _effectiveViewportRegistered;

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

                return new AnimatedImagePresentation(Source, width, height, scale, LimitFps, LoopCount, AutoPlay, IsCachingEnabled, resize);
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
                        _presenter.Unload(this, _state == PlayingState.Playing);
                        _presenter.LoopCompleted -= OnLoopCompleted;
                        _presenter.PositionChanged -= OnPositionChanged;
                        _presenter.Paused -= OnPaused;
                        _presenter = null;
                    }

                    _delayedPlay |= _state == PlayingState.Playing;
                    _state = PlayingState.None;
                    _clean = true;

                    UpdateShimmer(presentation?.Source);

                    if (presentation != null)
                    {
                        _presenter = AnimatedImageLoader.Current.GetOrCreate(presentation);
                        _presenter.LoopCompleted += OnLoopCompleted;
                        _presenter.PositionChanged += OnPositionChanged;
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
            // TODO: Enable whenever IsDownloadCompleted == false
            if (_clean is false || !IsConnected || !IsOutlineEnabled)
            {
                return;
            }

            if (source?.Outline != null)
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
            }
        }

        private void OnLoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            LoopCompleted?.Invoke(this, e);
        }

        private void OnPositionChanged(object sender, AnimatedImagePositionChangedEventArgs e)
        {
            PositionChanged?.Invoke(this, e);
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _delayedPlay = false;
            _state = PlayingState.Paused;
        }

        public virtual void Invalidate(ImageBrush source)
        {
            if (IsDisconnected)
            {
                return;
            }

            if (source != null || CleanOnSourceChanged)
            {
                LayoutRoot.Background = source;
            }

            if (_clean && source != null)
            {
                _clean = false;

                if (DominantColor is SolidColorBrush dominantColor)
                {
                    dominantColor.Color = GetDominantColor(source.ImageSource as WriteableBitmap);
                }

                if (UpdateRotation(source))
                {
                    source.Stretch = Stretch.None;
                }
                else
                {
                    source.Stretch = Stretch;
                }

                _shimmer = null;
                ElementCompositionPreview.SetElementChildVisual(LayoutRoot, null);

                Ready?.Invoke(this, EventArgs.Empty);

                if (ReplacementColor != null)
                {
                    ReplacementColorChanged(true);
                }
            }
        }

        private unsafe Color GetDominantColor(WriteableBitmap bitmap)
        {
            if (bitmap == null)
            {
                return Color.FromArgb(0x55, 0, 0, 0);
            }

            float stepH = (bitmap.PixelHeight - 1) / 10f;
            float stepW = (bitmap.PixelWidth - 1) / 10f;

            int width = bitmap.PixelWidth;
            bitmap.Buffer(out byte* imageBytes);

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

        private bool UpdateRotation(ImageBrush source)
        {
            if (LayoutRoot.Background is ImageBrush { ImageSource: WriteableBitmap bitmap, Transform: CompositeTransform composite })
            {
                double pixelWidth;
                double pixelHeight;

                if (composite.Rotation is 90 or 270)
                {
                    pixelWidth = bitmap.PixelHeight;
                    pixelHeight = bitmap.PixelWidth;
                }
                else
                {
                    pixelWidth = bitmap.PixelWidth;
                    pixelHeight = bitmap.PixelHeight;
                }

                var scaleX = ActualWidth / pixelWidth;
                var scaleY = ActualHeight / pixelHeight;
                var scale = Math.Max(scaleX, scaleY);

                composite.ScaleX = scale;
                composite.ScaleY = scale;

                composite.CenterX = ActualWidth / 2;
                composite.CenterY = ActualHeight / 2;

                return true;
            }

            return false;
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

        protected override void OnRasterizationScaleChanged(double rasterizationScale)
        {
            if (_rasterizationScale != rasterizationScale && DecodeFrameType == DecodePixelType.Logical)
            {
                _rasterizationScale = rasterizationScale;
                Load();
            }
        }

        #region ReplacementColor

        private bool _needsBrushUpdate;
        private Color _replacementColor;
        private long _replacementColorToken;
        private CompositionEffectBrush _effectBrush;

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
            if (_needsBrushUpdate || (_presenter?.Presentation.Source.NeedsRepainting is not true && _effectBrush == null))
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
                if (_effectBrush != null)
                {
                    LayoutRoot.Opacity = 1;
                    ElementCompositionPreview.SetElementChildVisual(this, null);
                }

                _effectBrush = null;
                return;
            }

            // This code mostly comes from MonochromaticOverlayPresenter

            _replacementColor = replacement.Color;

            if (_effectBrush != null)
            {
                try
                {
                    _effectBrush.Properties.InsertColor("Tint.Color", replacement.Color);
                    return;
                }
                catch (Exception ex)
                {
                    // If it throws, let's rebuild the brush
                    Logger.Exception(ex);
                }
            }

            try
            {
                var compositor = BootStrapper.Current.Compositor;

                // Build an effect that takes the source image and uses the alpha channel and replaces all other channels with
                // the ReplacementColor's RGB.
                var colorMatrixEffect = new ColorMatrixEffect();
                colorMatrixEffect.Source = new CompositionEffectSourceParameter("Source");
                var colorMatrix = new Matrix5x4();

                // If the ReplacementColor is not transparent then use the RGB values as the new color. Otherwise
                // just show the target by using an Identity colorMatrix.
                if (_replacementColor.A != 0)
                {
                    colorMatrix.M51 = colorMatrix.M52 = colorMatrix.M53 = colorMatrix.M44 = 1;
                }
                else
                {
                    colorMatrix.M11 = colorMatrix.M22 = colorMatrix.M33 = colorMatrix.M44 = 1;
                }

                colorMatrixEffect.ColorMatrix = colorMatrix;

                var tintEffect = new TintEffect();
                tintEffect.Name = "Tint";
                tintEffect.Source = colorMatrixEffect;
                tintEffect.Color = _replacementColor;

                var effectFactory = compositor.CreateEffectFactory(tintEffect, new[] { "Tint.Color" });

                var actualSize = FrameSize.ToVector2();
                var offset = Vector2.Zero;

                // Create a VisualSurface positioned at the same location as this control and feed that
                // through the color effect.
                var surfaceBrush = compositor.CreateSurfaceBrush();
                surfaceBrush.Stretch = CompositionStretch.None;
                var surface = compositor.CreateVisualSurface();

                // Select the source visual and the offset/size of this control in that element's space.
                surface.SourceVisual = ElementComposition.GetElementVisual(LayoutRoot);
                surface.SourceOffset = offset;
                surface.SourceSize = actualSize;
                surfaceBrush.Surface = surface;
                surfaceBrush.Stretch = CompositionStretch.None;

                _effectBrush = effectFactory.CreateBrush();
                _effectBrush.SetSourceParameter("Source", surfaceBrush);

                var visual = compositor.CreateSpriteVisual();
                visual.Size = actualSize;
                visual.Brush = _effectBrush;

                LayoutRoot.Opacity = 0;
                ElementCompositionPreview.SetElementChildVisual(this, visual);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
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
        private bool _renderingSubscribed;

        private AnimatedImageTask _task;
        private bool _requested;

        private volatile bool _rendering;
        private volatile bool _ticking;
        private volatile bool _disposing;
        private volatile bool _disposed;

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
        }

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

        public AnimatedImagePresentation Presentation => _presentation;

        public int CorrelationId { get; set; }

        public void Load(AnimatedImage canvas)
        {
            _images.Add(canvas);
            LoadImpl();

            if (_dirty)
            {
                canvas.Invalidate(_imageBrush);
            }
        }

        public void Unload(AnimatedImage canvas, bool playing)
        {
            _images.Remove(canvas);
            UnloadImpl(playing);

            canvas.Invalidate(null);
        }

        private void LoadImpl()
        {
            _loaded++;

            if (_loaded == 1 && !_requested)
            {
                _requested = true;

                if (_presentation.Source is DelayedFileSource delayed && !delayed.IsDownloadingCompleted)
                {
                    delayed.DownloadFile(this, DelayedFileDownload.Loaded, UpdateFile);
                }
                else
                {
                    _loader.Load(this);
                }
            }
        }

        private void UpdateFile(object target, Td.Api.File file)
        {
            if (_loaded > 0)
            {
                _loader.Load(this);
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
                _loader.Activated -= OnActivated;
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

            if (_dirty)
            {
                canvas.Invalidate(_imageBrush);
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
                    if (_presentation.Source is DelayedFileSource delayed && !delayed.IsDownloadingCompleted)
                    {
                        delayed.DownloadFile(this, DelayedFileDownload.Playing, UpdateFile);
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

                _rendering = true;
                RegisterRendering();

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
                        delayed.DownloadFile(this, DelayedFileDownload.Unloaded, UpdateFile);
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

                _rendering = true;

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
                _loader.Activated -= OnActivated;
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

        //private IBuffer _foregroundPrev;
        //private IBuffer _foregroundNext;
        //private IBuffer _backgroundNext;

        //private SurfaceImage _surface;

        private PixelBuffer _foregroundPrev;
        private PixelBuffer _foregroundNext;
        private PixelBuffer _backgroundNext;

        private ImageBrush _imageBrush;

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

            _foregroundPrev = new PixelBuffer(new WriteableBitmap(width, height));
            _backgroundNext = new PixelBuffer(new WriteableBitmap(width, height));

            _activated = Window.Current.CoreWindow.ActivationMode != CoreWindowActivationMode.Deactivated;

            // Automatically pause only if looping
            if (_presentation.LoopCount != 1)
            {
                _activated = true;
                _loader.Activated += OnActivated;
            }

            RegisterRendering();
        }

        private void InvokePaused()
        {
            Paused?.Invoke(this, EventArgs.Empty);

            _playing = 0;
            _pausedLock.Release();
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_disposed)
            {
                //UnregisterEvents();

                _loader.Activated -= OnActivated;
                return;
            }

            var activated = args.WindowActivationState != CoreWindowActivationState.Deactivated;
            var subscribe = Activated(activated);

            if (subscribe)
            {
                RegisterRendering();
            }
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
                    //_dispatcherQueue.TryEnqueue(RegisterRendering);

                    _rendering = true;
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

        private void RegisterRendering()
        {
            if (!_renderingSubscribed)
            {
                _renderingSubscribed = true;
                AnimatedImageLoader.Current.Rendering(this);
            }
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

                _rendering = false;

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

        private bool NextFrame(IBuffer frame)
        {
            var task = Volatile.Read(ref _task);
            if (task == null)
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
                Interlocked.Increment(ref _loopCount);

                _prevCompleted ??= new AnimatedImageLoopCompletedEventArgs();
                _prevCompleted.Cancel = false;

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

            Volatile.Write(ref _task, null);

            Interlocked.Exchange(ref _foregroundPrev, null);
            Interlocked.Exchange(ref _foregroundNext, null);
            Interlocked.Exchange(ref _backgroundNext, null);

            _loader.Activated -= OnActivated;
            _loader.Remove(_presentation);
        }

        //private double _targetIntervalTicks;
        //private long _lastTick;

        public bool Invalidate()
        {
            if (_images.Count > 0)
            {
                DrawFrame();

                //long now = Stopwatch.GetTimestamp();

                //if (_lastTick == 0 || now - _lastTick >= _targetIntervalTicks || !_rendering)
                //{
                //    _lastTick = now;
                //    DrawFrame();
                //}
            }

            if (!_rendering && _renderingSubscribed)
            {
                _renderingSubscribed = false;
                return true;
            }

            return false;
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

                //_surface ??= PlaceholderImageHelper.Current.Create(_task.PixelWidth, _task.PixelHeight);
                //PlaceholderImageHelper.Current.Invalidate(_surface, next);

                next.Source.Invalidate();

                if (_imageBrush == null)
                {
                    _imageBrush = new ImageBrush
                    {
                        Stretch = Stretch.Uniform,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center,
                    };

                    var task = Volatile.Read(ref _task);
                    if (task.Rotation != 0)
                    {
                        _imageBrush.Transform = new CompositeTransform
                        {
                            Rotation = task.Rotation
                        };
                    }
                }

                _imageBrush.ImageSource = next.Source;

                if (_dirty is false)
                {
                    foreach (var image in _images)
                    {
                        image.Invalidate(_imageBrush);
                    }
                }

                _dirty = true;
                _foregroundPrev = next;

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

            if (_animation.IsReadyToCache && !_shouldStop)
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

            var frameRate = Math.Clamp(animation.FrameRate, 1, 60 /*presentation.LimitFps ? 30 : 60*/);
            var interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

            Interval = interval;
            FrameRate = frameRate;
        }

        private int _index;

        public override AnimatedImageTaskState NextFrame(IBuffer frame, out double position)
        {
            position = 0;

            if (_animation.IsReadyToCache && !_shouldStop)
            {
                _animation.Cache();
                return AnimatedImageTaskState.Skip;
            }
            else if (_animation.IsCaching)
            {
                return AnimatedImageTaskState.Skip;
            }

            _animation.RenderSync(frame, out double seconds, out bool completed);
            _index++;

            if (_animation.TotalFrame == 1 || _shouldStop || (completed && _index == 1))
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

    public partial class WebpAnimatedImageTask : AnimatedImageTask
    {
        private readonly IBuffer _animation;

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

            BufferSurface.Copy(_animation, frame);
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

        public virtual void Seek(string marker)
        {

        }
    }

    public record AnimatedImagePresentation(AnimatedImageSource Source, int PixelWidth, int PixelHeight, double RasterizationScale, bool LimitFps, int LoopCount, bool AutoPlay, bool IsCachingEnabled, AnimatedImageResizeMode ResizeMode);

    public partial class AnimatedImageLoader
    {
        [ThreadStatic]
        private static AnimatedImageLoader _current;
        public static AnimatedImageLoader Current => _current ??= new();

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WindowContext _window;

        private bool _closed;

        private AnimatedImageLoader()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _window = WindowContext.Current;

            Debug.Assert(_dispatcherQueue != null);
        }

        public static void Release()
        {
            if (_current?._rendering.Count > 0)
            {
                _current._closed = true;
            }
            else
            {
                _current = null;
            }
        }

        private readonly List<AnimatedImagePresenter> _rendering = new();

        public void Rendering(AnimatedImagePresenter presenter)
        {
            if (_rendering.Count == 0)
            {
                Windows.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;
            }

            _rendering.Add(presenter);
        }

        public event EventHandler<WindowActivatedEventArgs> Activated
        {
            add => _window.Activated += value;
            remove => _window.Activated -= value;
        }

        private void OnRendering(object sender, object e)
        {
            for (int i = 0; i < _rendering.Count; i++)
            {
                if (_rendering[i].Invalidate())
                {
                    _rendering.RemoveAt(i--);
                }
            }

            if (_rendering.Count == 0)
            {
                Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;

                if (_closed)
                {
                    _current = null;
                }
            }
        }

        private readonly LifoActionWorker _workQueue = new();

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
            if (sender.CorrelationId != 0 && _delegates.ContainsKey(sender.CorrelationId))
            {
                // Already queued, don't enqueue again
                return;
            }

            var correlationId = ++_indexer;

            sender.CorrelationId = correlationId;

            _delegates[correlationId] = new WeakReference<AnimatedImagePresenter>(sender);
            _workQueue.Run(() => Work(new WorkItem(correlationId, sender.Presentation)));
        }

        private void Work(WorkItem work)
        {
            if (!_delegates.TryRemove(work.CorrelationId, out var weakDelegate))
            {
                return;
            }

            try
            {
                if (work.Presentation.Source is LocalFileSource local)
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

            var animation = PlaceholderImageHelper.DrawWebP(local.FilePath, work.Presentation.PixelWidth, out int pixelWidth, out int pixelHeight);
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

        class WorkQueue
        {
            private readonly object _workAvailable = new();
            private readonly Queue<WorkItem> _work = new();
            private bool _shutdown;

            public void Push(WorkItem item)
            {
                lock (_workAvailable)
                {
                    _work.Enqueue(item);
                    Monitor.Pulse(_workAvailable);
                }
            }

            public WorkItem WaitAndPop(int timeoutMs = 3000)
            {
                lock (_workAvailable)
                {
                    while (true)
                    {
                        if (_shutdown)
                        {
                            return null;
                        }

                        if (_work.TryDequeue(out WorkItem item))
                        {
                            return item;
                        }

                        if (!Monitor.Wait(_workAvailable, timeoutMs))
                        {
                            return null;
                        }
                    }
                }
            }

            public void Clear()
            {
                lock (_workAvailable)
                {
                    _shutdown = true;
                    _work.Clear();
                    Monitor.PulseAll(_workAvailable);
                }
            }
        }
    }
}
