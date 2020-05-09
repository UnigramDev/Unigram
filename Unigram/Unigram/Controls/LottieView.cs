using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasAnimatedControl))]
    public class LottieView : Control
    {
        private CanvasAnimatedControl Canvas;
        private string CanvasPartName = "Canvas";

        private int _frameWidth;
        private int _frameHeight;

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

        public LottieView()
        {
            DefaultStyleKey = typeof(LottieView);
        }

        ~LottieView()
        {
            Dispose();
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild(CanvasPartName) as CanvasAnimatedControl;
            if (canvas == null)
            {
                return;
            }

            Canvas = canvas;
            Canvas.Paused = true;
            Canvas.Unloaded += OnUnloaded;
            Canvas.CreateResources += OnCreateResources;
            Canvas.Draw += OnDraw;

            base.OnApplyTemplate();
        }

        public void Dispose()
        {
            if (_animation is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _animation = null;
            _source = null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();

            Canvas.Unloaded -= OnUnloaded;
            Canvas.CreateResources -= OnCreateResources;
            Canvas.Draw -= OnDraw;
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        private void OnCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            Dispose();

            if (args.Reason == CanvasCreateResourcesReason.FirstTime)
            {
                OnSourceChanged(UriToPath(Source), _source);
            }
        }

        private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            var animation = _animation;
            if (animation == null || _animationIsCaching)
            {
                return;
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            using (var bitmap = _animation.RenderSync(sender, index, 256, 256))
            {
                args.DrawingSession.DrawImage(bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));
            }

            if (!animation.IsCached)
            {
                _animationIsCached = true;
                _animationIsCaching = true;

                ThreadPool.QueueUserWorkItem(state =>
                {
                    if (state is CachedAnimation cached)
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
                        sender.Paused = true;
                        sender.ResetElapsedTime();
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
                    sender.Paused = true;
                    sender.ResetElapsedTime();
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

            _index = (int)Math.Ceiling(_animation.TotalFrame * position);
        }

        public int Ciccio => _animation.TotalFrame;
        public int Index => _index == int.MaxValue ? 0 : _index;

        private void OnSourceChanged(Uri newValue, Uri oldValue)
        {
            OnSourceChanged(UriToPath(newValue), UriToPath(oldValue));
        }

        private void OnSourceChanged(string newValue, string oldValue)
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                return;
            }

            if (newValue == null)
            {
                canvas.Paused = true;
                canvas.ResetElapsedTime();

                Dispose();
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var animation = CachedAnimation.LoadFromFile(newValue, true, _limitFps, ColorReplacements);
            if (animation == null)
            {
                // The app can't access the specified file
                return;
            }

            _source = newValue;
            _animation = animation;

            _animationIsCached = animation.IsCached;
            _animationFrameRate = animation.FrameRate;
            _animationTotalFrame = animation.TotalFrame;

            _index = 0;

            var update = TimeSpan.FromSeconds(_animation.Duration / _animation.TotalFrame);
            if (_limitFps && _animation.FrameRate >= 60)
            {
                update = TimeSpan.FromSeconds(update.TotalSeconds * 2);
            }

            canvas.Paused = true;
            canvas.ResetElapsedTime();
            canvas.TargetElapsedTime = update > TimeSpan.Zero ? update : TimeSpan.MaxValue;

            if (AutoPlay || _shouldPlay)
            {
                _shouldPlay = false;
                canvas.Paused = false;
            }

            // Invalidate to render the first frame
            canvas.Invalidate();
        }

        public void Play(bool backward = false)
        {
            var canvas = Canvas;
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

            canvas.Paused = false;
            canvas.Invalidate();
        }

        public void Pause()
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                //_source = newValue;
                return;
            }

            canvas.Paused = true;
            canvas.ResetElapsedTime();
        }

        public void Invalidate()
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                return;
            }

            canvas.Invalidate();
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

        #region FrameSize

        public Size FrameSize
        {
            get { return (Size)GetValue(FrameSizeProperty); }
            set { SetValue(FrameSizeProperty, value); }
        }

        public static readonly DependencyProperty FrameSizeProperty =
            DependencyProperty.Register("FrameSize", typeof(Size), typeof(LottieView), new PropertyMetadata(new Size(256, 256), OnFrameSizeChanged));

        private static void OnFrameSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._frameWidth = (int)((Size)e.NewValue).Width;
            ((LottieView)d)._frameHeight = (int)((Size)e.NewValue).Height;
        }

        #endregion

        public event EventHandler<double> PositionChanged;
        public event EventHandler<int> IndexChanged;

        public IReadOnlyDictionary<uint, uint> ColorReplacements { get; set; }
    }
}
