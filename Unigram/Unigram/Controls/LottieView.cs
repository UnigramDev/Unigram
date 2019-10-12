using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
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

        private string _source;
        private IAnimation _animation;

        private bool _isLoopingEnabled = true;
        private bool _isCachingEnabled = true;

        public LottieView()
        {
            DefaultStyleKey = typeof(LottieView);
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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _animation = null;
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
            if (_animation == null)
            {
                return;
            }

            var index = (int)(args.Timing.UpdateCount % _animation.TotalFrame);
            if (index == _animation.TotalFrame - 1 && !_isLoopingEnabled)
            {
                sender.Paused = true;
                sender.ResetElapsedTime();

                Dispose();
            }

            var bitmap = _animation.RenderSync(sender, index, 256, 256);
            args.DrawingSession.DrawImage(bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));
        }

        private void OnSourceChanged(Uri newValue, Uri oldValue)
        {
            OnSourceChanged(UriToPath(newValue), UriToPath(oldValue));
        }

        private void OnSourceChanged(string newValue, string oldValue)
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                //_source = newValue;
                return;
            }

            if (newValue == null)
            {
                canvas.Paused = true;
                canvas.ResetElapsedTime();

                _animation = null;
                Dispose();
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var animation = LoadFromFile(newValue, _isLoopingEnabled && _isCachingEnabled);
            if (animation == null)
            {
                // The app can't access the specified file
                return;
            }

            _source = newValue;
            _animation = animation;

            canvas.Paused = true;
            canvas.ResetElapsedTime();
            canvas.TargetElapsedTime = TimeSpan.FromSeconds(_animation.Duration / _animation.TotalFrame);

            if (AutoPlay)
            {
                Play();
            }
        }

        public void Play()
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                //_source = newValue;
                return;
            }

            var animation = _animation;
            if (animation == null)
            {
                return;
            }

            if (animation is CachedAnimation cached && !cached.IsCached)
            {
                cached.RenderSync(0, 256, 256);
                cached.CreateCache(256, 256);
            }

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

        private IAnimation LoadFromFile(string path, bool cache)
        {
            if (cache)
            {
                return CachedAnimation.LoadFromFile(path, true);
            }

            return Animation.LoadFromFile(path);
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
                    return uri.AbsolutePath.Replace('/', '\\');
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
    }
}
