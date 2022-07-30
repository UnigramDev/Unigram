using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public interface IPlayerView
    {
        bool Play();
        void Pause();

        void Unload();

        bool IsLoopingEnabled { get; }

        object Tag { get; set; }
    }

    public class LottieView30Fps : LottieView
    {
        public LottieView30Fps()
            : base(true)
        {

        }
    }

    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    public class LottieView : IndividualAnimatedControl<LottieAnimation>
    {
        private bool? _hideThumbnail;

        private string _source;

        private double _animationFrameRate;
        private int _animationTotalFrame;

        private int _index;
        private bool _backward;
        private bool _flipped;

        private bool _isCachingEnabled = true;

        private SizeInt32 _logicalSize = new SizeInt32 { Width = 256, Height = 256 };
        private SizeInt32 _frameSize = new SizeInt32 { Width = 256, Height = 256 };
        private DecodePixelType _decodeFrameType = DecodePixelType.Physical;

        public LottieView()
            : this(null)
        {
        }

        protected LottieView(bool? limitFps)
            : base(limitFps)
        {
            DefaultStyleKey = typeof(LottieView);
        }

        protected override void SourceChanged()
        {
            if (Source != null)
            {
                OnSourceChanged(UriToPath(Source), _source);
            }
        }

        protected override void Dispose()
        {
            if (_animation != null && !_animation.IsCaching)
            {
                _animation.Dispose();
            }

            _source = null;
            _animation = null;
        }

        protected override CanvasBitmap CreateBitmap(ICanvasResourceCreator sender)
        {
            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != _frameSize.Width || _bitmap?.Size.Height != _frameSize.Height;

            if (needsCreate)
            {
                if (_bitmap != null)
                {
                    _bitmap.Dispose();
                    _bitmap = null;
                }

                return CreateBitmap(sender, _frameSize.Width, _frameSize.Height);
            }

            return _bitmap;
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            if (_bitmap == null || _animation == null || _unloaded)
            {
                return;
            }

            if (_flipped)
            {
                args.Transform = Matrix3x2.CreateScale(-1, 1, sender.Size.ToVector2() / 2);
            }

            double width = _bitmap.Size.Width;
            double height = _bitmap.Size.Height;

            double ratioX = (double)sender.Size.Width / width;
            double ratioY = (double)sender.Size.Height / height;

            if (ratioX > ratioY)
            {
                width = sender.Size.Width;
                height *= ratioX;
            }
            else
            {
                width *= ratioY;
                height = sender.Size.Height;
            }

            var y = (sender.Size.Height - height) / 2;
            var x = (sender.Size.Width - width) / 2;

            if (sender.Size.Width >= _logicalSize.Width || sender.Size.Height >= _logicalSize.Height)
            {
                args.DrawImage(_bitmap,
                    new Rect(x, y, width, height));
            }
            else
            {
                args.DrawImage(_bitmap,
                    new Rect(x, y, width, height),
                    new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height), 1,
                    CanvasImageInterpolation.MultiSampleLinear);
            }

            if (_hideThumbnail == true)
            {
                _hideThumbnail = false;

                FirstFrameRendered?.Invoke(this, EventArgs.Empty);
                ElementCompositionPreview.SetElementChildVisual(this, null);
            }
        }

        protected override void NextFrame()
        {
            var animation = _animation;
            if (animation == null || animation.IsCaching || _bitmap == null || _unloaded)
            {
                return;
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            animation.RenderSync(_bitmap, index);

            IndexChanged?.Invoke(this, index);
            PositionChanged?.Invoke(this, Math.Min(1, Math.Max(0, (double)index / (_animationTotalFrame - 1))));

            if (_hideThumbnail == null)
            {
                _hideThumbnail = true;
            }

            if (_backward)
            {
                if (index - framesPerUpdate >= 0)
                {
                    _index -= framesPerUpdate;
                }
                else
                {
                    _index = 0;
                    _backward = false;

                    if (!_isLoopingEnabled)
                    {
                        Subscribe(false);
                    }
                }
            }
            else
            {
                if (index + framesPerUpdate < _animationTotalFrame)
                {
                    _index += framesPerUpdate;
                }
                else
                {
                    if (!_isLoopingEnabled)
                    {
                        Subscribe(false);
                    }
                    else
                    {
                        _index = 0;
                    }
                }
            }
        }

        public void Seek(double position)
        {
            if (position is < 0 or > 1)
            {
                return;
            }

            var animation = _animation;
            if (animation == null)
            {
                return;
            }

            _index = (int)Math.Min(_animation.TotalFrame - 1, Math.Ceiling((_animation.TotalFrame - 1) * position));
        }

        public int Index => _index == int.MaxValue ? 0 : _index;

        public double Offset => _index == int.MaxValue || _animation == null ? 0 : (double)_index / (_animation.TotalFrame - 1);

        private void OnSourceChanged(Uri newValue, Uri oldValue)
        {
            OnSourceChanged(UriToPath(newValue), UriToPath(oldValue));
        }

        private async void OnSourceChanged(string newValue, string oldValue)
        {
            //var canvas = _canvas;
            //if (canvas == null && !Load())
            //{
            //    return;
            //}

            if (newValue == null)
            {
                Unload();
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _source = newValue;

            var animation = await Task.Run(() => LottieAnimation.LoadFromFile(newValue, _frameSize, _isCachingEnabled, ColorReplacements, FitzModifier));
            if (animation == null || !string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                // The app can't access the file specified
                return;
            }

            var frameRate = Math.Clamp(animation.FrameRate, 30, _limitFps ? 30 : 60);

            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));
            _animation = animation;
            _hideThumbnail = null;

            _animationFrameRate = animation.FrameRate;
            _animationTotalFrame = animation.TotalFrame;

            if (_backward)
            {
                _index = _animationTotalFrame - 1;
            }
            else
            {
                _index = 0; //_isCachingEnabled ? 0 : _animationTotalFrame - 1;
            }

            OnSourceChanged();
        }

        public async Task UpdateColorsAsync(IReadOnlyDictionary<int, int> colorReplacements)
        {
            var newValue = _source;

            var animation = await Task.Run(() => LottieAnimation.LoadFromFile(_source, _frameSize, _isCachingEnabled, colorReplacements, FitzModifier));
            if (animation == null || !string.Equals(newValue, _source, StringComparison.OrdinalIgnoreCase))
            {
                // The app can't access the file specified
                return;
            }

            _animation = animation;
            ColorReplacements = colorReplacements;
        }

        public bool Play(bool backward = false)
        {
            _playing = true;
            Load();

            var animation = _animation;
            if (animation == null)
            {
                return false;
            }

            _backward = backward;

            if (_canvas == null || _subscribed || !_active)
            {
                return false;
            }

            if (_index >= animation.TotalFrame - 2 && !_isLoopingEnabled && !_backward)
            {
                _index = 0;
            }

            if (_canvas.IsLoaded || _layoutRoot.IsLoaded)
            {
                Subscribe(true);
            }

            return true;
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
                    return Path.Combine(new[] { Package.Current.InstalledLocation.Path }.Union(uri.Segments.Select(x => x.Trim('/'))).ToArray());
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

        #region IsCachingEnabled

        public bool IsCachingEnabled
        {
            get => (bool)GetValue(IsCachingEnabledProperty);
            set => SetValue(IsCachingEnabledProperty, value);
        }

        public static readonly DependencyProperty IsCachingEnabledProperty =
            DependencyProperty.Register("IsCachingEnabled", typeof(bool), typeof(LottieView), new PropertyMetadata(true, OnCachingEnabledChanged));

        private static void OnCachingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._isCachingEnabled = (bool)e.NewValue;
        }

        #endregion

        #region IsBackward

        public bool IsBackward
        {
            get => (bool)GetValue(IsBackwardProperty);
            set => SetValue(IsBackwardProperty, value);
        }

        public static readonly DependencyProperty IsBackwardProperty =
            DependencyProperty.Register("IsBackward", typeof(bool), typeof(LottieView), new PropertyMetadata(false, OnBackwardChanged));

        private static void OnBackwardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._backward = (bool)e.NewValue;
        }

        #endregion

        #region IsFlipped

        public bool IsFlipped
        {
            get => (bool)GetValue(IsFlippedProperty);
            set => SetValue(IsFlippedProperty, value);
        }

        public static readonly DependencyProperty IsFlippedProperty =
            DependencyProperty.Register("IsFlipped", typeof(bool), typeof(LottieView), new PropertyMetadata(false, OnFlippedChanged));

        private static void OnFlippedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d)._flipped = (bool)e.NewValue;
        }

        #endregion

        #region FrameSize

        public Size FrameSize
        {
            get => (Size)GetValue(FrameSizeProperty);
            set => SetValue(FrameSizeProperty, value);
        }

        public static readonly DependencyProperty FrameSizeProperty =
            DependencyProperty.Register("FrameSize", typeof(Size), typeof(LottieView), new PropertyMetadata(new Size(256, 256), OnFrameSizeChanged));

        private static void OnFrameSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d).OnFrameSizeChanged((Size)e.NewValue, ((LottieView)d)._decodeFrameType);
        }

        #endregion

        #region DecodeFrameType

        public DecodePixelType DecodeFrameType
        {
            get { return (DecodePixelType)GetValue(DecodeFrameTypeProperty); }
            set { SetValue(DecodeFrameTypeProperty, value); }
        }

        public static readonly DependencyProperty DecodeFrameTypeProperty =
            DependencyProperty.Register("DecodeFrameType", typeof(DecodePixelType), typeof(LottieView), new PropertyMetadata(DecodePixelType.Physical, OnDecodeFrameTypeChanged));

        private static void OnDecodeFrameTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d).OnFrameSizeChanged(((LottieView)d)._frameSize, (DecodePixelType)e.NewValue);
        }

        #endregion

        private void OnFrameSizeChanged(SizeInt32 frameSize, DecodePixelType decodeFrameType)
        {
            OnFrameSizeChanged(new Size(frameSize.Width, frameSize.Height), decodeFrameType);
        }

        private void OnFrameSizeChanged(Size frameSize, DecodePixelType decodeFrameType)
        {
            if (decodeFrameType == DecodePixelType.Logical)
            {
                // TODO: subscribe for DPI changed event
                var dpi = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;

                _decodeFrameType = decodeFrameType;
                _logicalSize = new SizeInt32
                {
                    Width = (int)frameSize.Width,
                    Height = (int)frameSize.Height
                };
                _frameSize = new SizeInt32
                {
                    Width = (int)(frameSize.Width * dpi),
                    Height = (int)(frameSize.Height * dpi)
                };
            }
            else
            {
                _decodeFrameType = decodeFrameType;
                _logicalSize = new SizeInt32
                {
                    Width = (int)frameSize.Width,
                    Height = (int)frameSize.Height
                };
                _frameSize = new SizeInt32
                {
                    Width = (int)frameSize.Width,
                    Height = (int)frameSize.Height
                };
            }
        }

        #region Source

        public Uri Source
        {
            get => (Uri)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(LottieView), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LottieView)d).OnSourceChanged((Uri)e.NewValue, (Uri)e.OldValue);
        }

        #endregion

        public event EventHandler<double> PositionChanged;
        public event EventHandler<int> IndexChanged;

        public event EventHandler FirstFrameRendered;

        public IReadOnlyDictionary<int, int> ColorReplacements { get; set; }

        public FitzModifier FitzModifier { get; set; }
    }
}
