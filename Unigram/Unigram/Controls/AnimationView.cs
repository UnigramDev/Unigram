using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Native;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    [TemplatePart(Name = "Thumbnail", Type = typeof(Image))]
    public class AnimationView : Control
    {
        private CanvasControl _canvas;
        private CanvasBitmap _bitmap;

        private Image _thumbnail;
        private bool? _hideThumbnail;

        private string _source;
        private VideoAnimation _animation;

        private bool _shouldPlay;

        private bool _isLoopingEnabled = true;

        private LoopThread _thread = LoopThreadPool.Animations.Get();
        private bool _subscribed;

        private ICanvasResourceCreator _device;

        public AnimationView()
        {
            DefaultStyleKey = typeof(AnimationView);
        }

        //~AnimationView()
        //{
        //    Dispose();
        //}

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild("Canvas") as CanvasControl;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;
            _canvas.CreateResources += OnCreateResources;
            _canvas.Draw += OnDraw;
            _canvas.Unloaded += OnUnloaded;

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

            _device = null;

            //_animation?.Dispose();
            _animation = null;

            //_bitmap?.Dispose();
            //_bitmap?.Dispose();
            _bitmap = null;
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

        private static object _reusableLock = new object();
        private static byte[] _reusableBuffer;

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(Task.Run(() =>
            {
                var animation = VideoAnimation.LoadFromFile(_source, false, true);
                if (animation == null)
                {
                    return;
                }

                _animation = animation;

                lock (_reusableLock)
                {
                    if (_reusableBuffer == null || _reusableBuffer.Length < _animation.PixelWidth * _animation.PixelHeight * 4)
                    {
                        _reusableBuffer = new byte[_animation.PixelWidth * _animation.PixelHeight * 4];
                    }
                }

                _bitmap = CanvasBitmap.CreateFromBytes(sender, _reusableBuffer, _animation.PixelWidth, _animation.PixelHeight, DirectXPixelFormat.R8G8B8A8UIntNormalized);
                _device = sender;

            }).AsAsyncAction());
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _device = args.DrawingSession.Device;

            if (_animation == null)
            {
                return;
            }

            var width = (double)_animation.PixelWidth;
            var height = (double)_animation.PixelHeight;
            var x = 0d;
            var y = 0d;

            //if (width > sender.Size.Width || height > sender.Size.Height)
            {
                double ratioX = (double)sender.Size.Width / width;
                double ratioY = (double)sender.Size.Height / height;

                if (ratioX > ratioY)
                {
                    width = sender.Size.Width;
                    height *= ratioX;
                    y = (sender.Size.Height - height) / 2;
                }
                else
                {
                    width *= ratioY;
                    height = sender.Size.Height;
                    x = (sender.Size.Width - width) / 2;
                }
            }

            args.DrawingSession.DrawImage(_bitmap, new Rect(x, y, width, height));

            if (_hideThumbnail == true && _thumbnail != null)
            {
                _hideThumbnail = false;
                _thumbnail.Opacity = 0;
            }
        }

        public void Invalidate()
        {
            var animation = _animation;
            if (animation == null || _canvas == null || _bitmap == null)
            {
                return;
            }

            //_bitmap = animation.RenderSync(_device, index, 256, 256);
            animation.RenderSync(_bitmap, false);

            if (_hideThumbnail == null)
            {
                _hideThumbnail = true;
            }
        }

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

            _source = newValue;

            if (AutoPlay || _shouldPlay)
            {
                _shouldPlay = false;
                Subscribe(true);
            }
            else
            {
                Subscribe(false);

                //// Invalidate to render the first frame
                //Invalidate();
                //_canvas.Invalidate();
            }
        }

        public void Play()
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
            LoopThread.Animations.Invalidate -= OnInvalidate;

            if (subscribe)
            {
                _thread.Tick += OnTick;
                LoopThread.Animations.Invalidate += OnInvalidate;
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
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(AnimationView), new PropertyMetadata(true, OnLoopingEnabledChanged));

        private static void OnLoopingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimationView)d)._isLoopingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(AnimationView), new PropertyMetadata(true));

        #endregion

        #region Source

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(AnimationView), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimationView)d).OnSourceChanged((Uri)e.NewValue, (Uri)e.OldValue);
        }

        #endregion

        #region Thumbnail

        public ImageSource Thumbnail
        {
            get { return (ImageSource)GetValue(ThumbnailProperty); }
            set { SetValue(ThumbnailProperty, value); }
        }

        public static readonly DependencyProperty ThumbnailProperty =
            DependencyProperty.Register("Thumbnail", typeof(ImageSource), typeof(AnimationView), new PropertyMetadata(null));

        #endregion

    }
}
