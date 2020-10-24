using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Views;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    [TemplatePart(Name = "Thumbnail", Type = typeof(Image))]
    public class DiceView : Control, IPlayerView
    {
        private CanvasControl _canvas;
        private CanvasBitmap[] _bitmaps;

        private Image _thumbnail;
        private bool _hideThumbnail = true;

        private const int _parts = 3;

        private int _enqueued = -1;
        private DiceStickers _enqueuedState;

        private int _value = -1;
        private DiceStickers _valueState;

        private int _previous = -1;
        private DiceStickers _previousState;

        private LottieAnimation[] _animations;

        private LottieAnimation _frontAnimation;
        private LottieAnimation _backAnimation;

        private double _animationFrameRate;
        private int _animationTotalFrame;

        private bool _shouldPlay;

        // Detect from hardware?
        private bool _limitFps = true;

        private bool _skipFrame;

        private int[] _index = new int[_parts];
        private int[] _startIndex = new int[_parts];

        private bool[] _isLoopingEnabled = new bool[_parts];

        private SizeInt32 _frameSize = new SizeInt32 { Width = 256, Height = 256 };

        private LoopThread _thread;
        private LoopThread _threadUI;
        private bool _subscribed;

        private ICanvasResourceCreator _device;

        public DiceView()
            : this(CompositionCapabilities.GetForCurrentView().AreEffectsFast())
        {
        }

        public DiceView(bool fullFps)
        {
            _limitFps = !fullFps;
            _thread = fullFps ? LoopThread.Chats : LoopThreadPool.Stickers.Get();
            _threadUI = fullFps ? LoopThread.Chats : LoopThread.Stickers;

            DefaultStyleKey = typeof(DiceView);
        }

        //~DiceView()
        //{
        //    Dispose();
        //}

        public bool IsUnloaded { get; private set; }

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

            OnValueChanged(_previousState, _previous);

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
            _valueState = null;
            _enqueuedState = null;
            _previousState = null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            IsUnloaded = true;

            Subscribe(false);

            _canvas.CreateResources -= OnCreateResources;
            _canvas.Draw -= OnDraw;
            _canvas.Unloaded -= OnUnloaded;
            _canvas.RemoveFromVisualTree();
            _canvas = null;

            Dispose();

            //_animation?.Dispose();
            _animations = null;

            //_bitmap?.Dispose();
            _bitmaps = null;
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
            lock (_reusableLock)
            {
                if (_reusableBuffer == null)
                {
                    _reusableBuffer = new byte[256 * 256 * 4];
                }
            }

            _device = sender;
            _bitmaps = new CanvasBitmap[_parts];

            //for (int i = 0; i < _bitmaps.Length; i++)
            //{
            //    _bitmaps[i] = CanvasBitmap.CreateFromBytes(sender, _reusableBuffer, _frameSize.Width, _frameSize.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
            //}

            if (args.Reason == CanvasCreateResourcesReason.FirstTime)
            {
                OnValueChanged(_previousState, _previous);
                Invalidate();
            }
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _device = args.DrawingSession.Device;

            if (_bitmaps != null)
            {
                for (int i = 0; i < _bitmaps.Length; i++)
                {
                    if (_bitmaps[i] != null)
                    {
                        args.DrawingSession.DrawImage(_bitmaps[i], new Rect(0, 0, sender.Size.Width, sender.Size.Height));
                    }
                    else if (i == 1)
                    {
                        return;
                    }
                }

                if (_hideThumbnail && _thumbnail != null)
                {
                    _hideThumbnail = false;
                    _thumbnail.Opacity = 0;
                }
            }
        }

        public void Invalidate()
        {
            var animations = _animations;
            if (animations == null || _canvas == null || _device == null)
            {
                return;
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            var enqueue = false;

            if (_animationFrameRate < 60 && !_limitFps)
            {
                if (_skipFrame)
                {
                    _skipFrame = false;
                    return;
                }

                _skipFrame = true;
            }

            for (int i = 0; i < animations.Length; i++)
            {
                if (_bitmaps[i] == null)
                {
                    if (animations[i] != null)
                    {
                        _bitmaps[i] = CanvasBitmap.CreateFromBytes(_device, _reusableBuffer, _frameSize.Width, _frameSize.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                    else
                    {
                        continue;
                    }
                }

                animations[i].RenderSync(_bitmaps[i], index[i]);

                if (i == 1 && !_isLoopingEnabled[i])
                {
                    IndexChanged?.Invoke(this, index[i]);
                }

                if (_startIndex[i] <= index[1] && index[i] + framesPerUpdate < animations[i].TotalFrame)
                {
                    _index[i] += framesPerUpdate;
                }
                else
                {
                    if (_isLoopingEnabled[i])
                    {
                        _index[i] = 0;

                        if (i == 1 && _value == 0 && _enqueued != 0 && _enqueuedState != null)
                        {
                            enqueue = true;
                        }
                    }
                    else if (i == 1)
                    {
                        _subscribed = false;
                        _ = Dispatcher.RunIdleAsync(idle => Subscribe(false));
                    }
                }
            }

            if (enqueue)
            {
                _subscribed = false;
                _ = Dispatcher.RunIdleAsync(idle => OnValueChanged(_enqueuedState, _enqueued));
            }
        }

        //public int Ciccio => _animationTotalFrame;
        //public int Index => _index == int.MaxValue ? 0 : _index;
        public bool IsLoopingEnabled => _isLoopingEnabled[1];

        private bool ValueEquals(MessageDice x, MessageDice y)
        {
            if (x?.Emoji == y?.Emoji && x?.Value == y?.Value)
            {
                return true;
            }

            return false;
        }

        public void SetValue(DiceStickers state, int value)
        {
            OnValueChanged(state, value);
        }

        private async void OnValueChanged(DiceStickers state, int newValue)
        {
            var canvas = _canvas;
            if (canvas == null)
            {
                _previous = newValue;
                _previousState = state;
                return;
            }

            _previous = 0;
            _previousState = null;

            if (state == null)
            {
                //canvas.Paused = true;
                //canvas.ResetElapsedTime();
                Subscribe(false);

                Dispose();
                return;
            }

            if (newValue == _value)
            {
                return;
            }

            if (newValue != _value && /*newValue != _enqueued &&*/ _value == 0)
            {
                if (_subscribed)
                {
                    _shouldPlay = true;
                    _enqueued = newValue;
                    _enqueuedState = state;
                    return;
                }
            }

            var force = _enqueued == newValue;

            _value = newValue;
            _valueState = state;

            _enqueued = 0;
            _enqueuedState = null;

            var initial = newValue == 0;

            var animations = new LottieAnimation[_parts];
            await Task.Run(() =>
            {
                if (state is DiceStickersSlotMachine slotMachine)
                {
                    animations[0] = _backAnimation ??= LottieAnimation.LoadFromFile(slotMachine.Background.StickerValue.Local.Path, false, null);
                    animations[1] = LottieAnimation.LoadFromData(MergeReels(slotMachine), $"{newValue}", false, null);
                    animations[2] = _frontAnimation ??= LottieAnimation.LoadFromFile(slotMachine.Lever.StickerValue.Local.Path, false, null);
                }
                else if (state is DiceStickersRegular regular)
                {
                    animations[1] = LottieAnimation.LoadFromFile(regular.Sticker.StickerValue.Local.Path, false, null);
                }
            });

            _animations = animations;
            _isLoopingEnabled[1] = initial;

            _animationFrameRate = animations.Max(x => x?.FrameRate ?? 0);
            _animationTotalFrame = animations.Max(x => x?.TotalFrame ?? 0);

            _startIndex[0] = _animationTotalFrame;

            _index[0] = 1;
            _index[1] = IsContentUnread || initial ? 0 : animations[1].TotalFrame - 1;
            _index[2] = initial ? 0 : _index[2];

            //canvas.Paused = true;
            //canvas.ResetElapsedTime();
            //canvas.TargetElapsedTime = update > TimeSpan.Zero ? update : TimeSpan.MaxValue;

            if (AutoPlay || _shouldPlay || force)
            {
                _shouldPlay = false;
                Subscribe(true);
                //canvas.Paused = false;
            }
            else
            {
                Subscribe(false);

                // Invalidate to render the first frame
                Invalidate();
                _canvas?.Invalidate();
            }
        }

        private string MergeReels(DiceStickersSlotMachine slotMachine)
        {
            var stopwatch = Stopwatch.StartNew();

            var l_part = JsonObject.Parse(DecompressReel(slotMachine.LeftReel.StickerValue.Local.Path));
            var c_part = JsonObject.Parse(DecompressReel(slotMachine.CenterReel.StickerValue.Local.Path));
            var r_part = JsonObject.Parse(DecompressReel(slotMachine.RightReel.StickerValue.Local.Path));

            var array = new[] { c_part, r_part };

            var assets = l_part.GetNamedArray("assets");
            var layers = l_part.GetNamedArray("layers");

            foreach (var part in array)
            {
                var name = part.GetNamedString("nm");

                foreach (var asset in part.GetNamedArray("assets").Select(x => x.GetObject()))
                {
                    asset.SetNamedValue("id", Windows.Data.Json.JsonValue.CreateStringValue($"{name}_{asset.GetNamedString("id")}"));
                    assets.Add(asset);
                }

                foreach (var layer in part.GetNamedArray("layers").Select(x => x.GetObject()))
                {
                    if (layer.TryGetValue("refId", out var refId))
                    {
                        layer.SetNamedValue("refId", Windows.Data.Json.JsonValue.CreateStringValue($"{name}_{refId.GetString()}"));
                    }

                    layers.Add(layer);
                }
            }

            stopwatch.Stop();

            return l_part.ToString();
        }

        private string DecompressReel(string path)
        {
            using (Stream fd = new MemoryStream())
            using (Stream fs = System.IO.File.OpenRead(path))
            using (Stream csStream = new GZipStream(fs, CompressionMode.Decompress))
            {
                byte[] buffer = new byte[1024];
                int nRead;
                while ((nRead = csStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fd.Write(buffer, 0, nRead);
                }

                fd.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(fd))
                {
                    return reader.ReadToEnd();
                }
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

            var animations = _animations;
            if (animations == null)
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
            _threadUI.Invalidate -= OnInvalidate;

            if (subscribe)
            {
                _thread.Tick += OnTick;
                _threadUI.Invalidate += OnInvalidate;
            }
        }

        #region FrameSize

        public SizeInt32 FrameSize
        {
            get { return (SizeInt32)GetValue(FrameSizeProperty); }
            set { SetValue(FrameSizeProperty, value); }
        }

        public static readonly DependencyProperty FrameSizeProperty =
            DependencyProperty.Register("FrameSize", typeof(SizeInt32), typeof(DiceView), new PropertyMetadata(new SizeInt32 { Width = 256, Height = 256 }, OnFrameSizeChanged));

        private static void OnFrameSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DiceView)d)._frameSize = (SizeInt32)e.NewValue;
        }

        #endregion

        #region AutoPlay

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(DiceView), new PropertyMetadata(true));

        #endregion

        #region IsContentUnread

        public bool IsContentUnread
        {
            get { return (bool)GetValue(IsContentUnreadProperty); }
            set { SetValue(IsContentUnreadProperty, value); }
        }

        public static readonly DependencyProperty IsContentUnreadProperty =
            DependencyProperty.Register("IsContentUnread", typeof(bool), typeof(DiceView), new PropertyMetadata(false));

        #endregion

        #region Thumbnail

        public ImageSource Thumbnail
        {
            get { return (ImageSource)GetValue(ThumbnailProperty); }
            set { SetValue(ThumbnailProperty, value); }
        }

        public static readonly DependencyProperty ThumbnailProperty =
            DependencyProperty.Register("Thumbnail", typeof(ImageSource), typeof(DiceView), new PropertyMetadata(null));

        #endregion

        public event EventHandler<int> IndexChanged;
    }
}
