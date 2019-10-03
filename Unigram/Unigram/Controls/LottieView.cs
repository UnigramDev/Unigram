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
        private Animation _animation;
        private CanvasBitmap[] _cache;

        private int _index;

        private bool _isLoopingEnabled = true;

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
            var cache = _cache;
            if (cache != null)
            {
                for (int i = 0; i < cache.Length; i++)
                {
                    if (cache[i] != null)
                    {
                        cache[i].Dispose();
                        cache[i] = null;
                    }
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var canvas = Canvas;
            if (canvas != null)
            {
                canvas.RemoveFromVisualTree();
                Canvas = null;
            }

            Dispose();
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
            if (_animation == null || _cache == null || _animation.TotalFrame != _cache.Length)
            {
                return;
            }

            var index = (int)(args.Timing.UpdateCount % _animation.TotalFrame);
            if (index == _animation.TotalFrame - 1 && !_isLoopingEnabled)
            {
                sender.Paused = true;
                sender.ResetElapsedTime();
            }

            if (_cache[index] == null)
            {
                var bytes = _animation.RenderSync(index);
                _cache[index] = CanvasBitmap.CreateFromBytes(sender, bytes, 512, 512, DirectXPixelFormat.B8G8R8A8UIntNormalized);
            }

            args.DrawingSession.DrawImage(_cache[index], new Rect(0, 0, 160, 160));
            _index = (_index + 1) % _animation.TotalFrame;
        }

        private void OnSourceChanged(string newValue, string oldValue)
        {
            if (newValue == null)
            {
                // TODO
                return;
            }

            if (newValue == oldValue)
            {
                return;
            }

            var canvas = Canvas;
            if (canvas == null)
            {
                return;
            }

            _source = newValue;
            _animation = Animation.LoadFromFile(newValue);
            _cache = new CanvasBitmap[_animation.TotalFrame];

            canvas.Paused = true;
            canvas.TargetElapsedTime = TimeSpan.FromSeconds(_animation.Duration / _animation.TotalFrame);

            if (AutoPlay)
            {
                canvas.Paused = false;
                canvas.ResetElapsedTime();
                canvas.Invalidate();
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
            DependencyProperty.Register("Source", typeof(Uri), typeof(LottieView), new PropertyMetadata(null));

        #endregion
    }
}
