//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    public class DiceView : Control, IPlayerView
    {
        private CanvasControl _canvas;
        private CanvasBitmap[] _bitmaps;

        private Grid _layoutRoot;

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
        private readonly bool _limitFps = true;

        private bool _skipFrame;

        private readonly int[] _index = new int[_parts];
        private readonly int[] _startIndex = new int[_parts];

        private readonly bool[] _isLoopingEnabled = new bool[_parts];

        private SizeInt32 _frameSize = new SizeInt32 { Width = 256, Height = 256 };

        private readonly LoopThread _thread;
        private readonly LoopThread _threadUI;
        private readonly object _subscribeLock = new object();
        private bool _subscribed;
        private bool _unsubscribe;

        private bool _unloaded;

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

            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _layoutRoot.Loading += OnLoading;
            _layoutRoot.Loaded += OnLoaded;
            _layoutRoot.Unloaded += OnUnloaded;

            SetValue(_previousState, _previous);

            base.OnApplyTemplate();
        }

        private bool Load()
        {
            if (_unloaded && _layoutRoot != null && _layoutRoot.IsLoaded)
            {
                while (_layoutRoot.Children.Count > 0)
                {
                    _layoutRoot.Children.Remove(_layoutRoot.Children[0]);
                }

                _canvas = new CanvasControl();
                _canvas.CreateResources += OnCreateResources;
                _canvas.Draw += OnDraw;
                _canvas.Unloaded += OnUnloaded;

                _layoutRoot.Children.Add(_canvas);

                _unloaded = false;
                SetValue(_previousState, _previous);

                return true;
            }

            return false;
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            Load();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        public void Unload()
        {
            _shouldPlay = false;
            _unloaded = true;
            Subscribe(false);

            if (_canvas != null)
            {
                _canvas.CreateResources -= OnCreateResources;
                _canvas.Draw -= OnDraw;
                _canvas.RemoveFromVisualTree();
                _canvas = null;
            }

            _valueState = null;

            //_bitmap?.Dispose();
            _bitmaps = null;

            //_animation?.Dispose();
            _animations = null;
        }

        private void OnTick(object sender, EventArgs args)
        {
            try
            {
                Invalidate();
            }
            catch
            {
                lock (_subscribeLock)
                {
                    _unsubscribe = true;
                    _thread.Tick -= OnTick;
                }
            }
        }

        private void OnInvalidate(object sender, EventArgs e)
        {
            _canvas?.Invalidate();
        }

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            _bitmaps = new CanvasBitmap[_parts];

            if (args.Reason == CanvasCreateResourcesReason.FirstTime)
            {
                SetValue(_previousState, _previous);
                Invalidate();
            }
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
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

                if (_hideThumbnail)
                {
                    _hideThumbnail = false;
                    FirstFrameRendered?.Invoke(this, EventArgs.Empty);
                }
            }

            Monitor.Enter(_subscribeLock);
            if (_unsubscribe)
            {
                _unsubscribe = false;
                Monitor.Exit(_subscribeLock);
                Subscribe(false);
            }
            else
            {
                Monitor.Exit(_subscribeLock);
            }
        }

        public void Invalidate()
        {
            var animations = _animations;
            if (animations == null || _canvas == null || _bitmaps == null)
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
                if (_bitmaps[i] == null || animations[i] == null)
                {
                    if (animations[i] != null)
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(256 * 256 * 4);
                        _bitmaps[i] = CanvasBitmap.CreateFromBytes(_canvas, buffer, _frameSize.Width, _frameSize.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                        ArrayPool<byte>.Shared.Return(buffer);
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
                        lock (_subscribeLock)
                        {
                            _subscribed = false;
                            _unsubscribe = true;
                            _thread.Tick -= OnTick;
                        }
                    }
                }
            }

            if (enqueue)
            {
                _subscribed = false;
                _ = Dispatcher.RunIdleAsync(idle => SetValue(_enqueuedState, _enqueued));
            }
        }

        //public int Ciccio => _animationTotalFrame;
        //public int Index => _index == int.MaxValue ? 0 : _index;
        public bool IsLoopingEnabled => _isLoopingEnabled[1];

        public async void SetValue(DiceStickers state, int newValue)
        {
            var canvas = _canvas;
            if (canvas == null  && !Load())
            {
                _previous = newValue;
                _previousState = state;
                return;
            }

            if (state == null)
            {
                //canvas.Paused = true;
                //canvas.ResetElapsedTime();
                Subscribe(false);

                //Dispose();
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

            _previous = newValue;
            _previousState = state;

            _enqueued = 0;
            _enqueuedState = null;

            _hideThumbnail = true;

            var initial = newValue == 0;
            var shouldPlay = _shouldPlay;

            var animations = new LottieAnimation[_parts];
            await Task.Run(() =>
            {
                if (state is DiceStickersSlotMachine slotMachine)
                {
                    animations[0] = _backAnimation ??= LottieAnimation.LoadFromFile(slotMachine.Background.StickerValue.Local.Path, _frameSize, false, null);
                    animations[1] = LottieAnimation.LoadFromData(MergeReels(slotMachine), _frameSize, $"{newValue}", false, null);
                    animations[2] = _frontAnimation ??= LottieAnimation.LoadFromFile(slotMachine.Lever.StickerValue.Local.Path, _frameSize, false, null);
                }
                else if (state is DiceStickersRegular regular)
                {
                    animations[1] = LottieAnimation.LoadFromFile(regular.Sticker.StickerValue.Local.Path, _frameSize, false, null);
                }
            });

            if (_shouldPlay)
            {
                shouldPlay = true;
            }

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

            if (AutoPlay || shouldPlay || force)
            {
                _shouldPlay = false;
                Subscribe(true);
                //canvas.Paused = false;
            }
            else if (!_unloaded)
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

        public bool Play()
        {
            Load();

            var canvas = _canvas;
            if (canvas == null)
            {
                _shouldPlay = true;
                return false;
            }

            var animations = _animations;
            if (animations == null)
            {
                _shouldPlay = true;
                return false;
            }

            _shouldPlay = false;

            if (_subscribed)
            {
                return false;
            }

            //canvas.Paused = false;
            Subscribe(true);
            return true;
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
            lock (_subscribeLock)
            {
                if (subscribe)
                {
                    _unsubscribe = false;
                }

                _subscribed = subscribe;
                _thread.Tick -= OnTick;
                _threadUI.Invalidate -= OnInvalidate;

                if (subscribe)
                {
                    _thread.Tick += OnTick;
                    _threadUI.Invalidate += OnInvalidate;
                }
            }
        }

        #region FrameSize

        public SizeInt32 FrameSize
        {
            get => (SizeInt32)GetValue(FrameSizeProperty);
            set => SetValue(FrameSizeProperty, value);
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
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(DiceView), new PropertyMetadata(true));

        #endregion

        #region IsContentUnread

        public bool IsContentUnread
        {
            get => (bool)GetValue(IsContentUnreadProperty);
            set => SetValue(IsContentUnreadProperty, value);
        }

        public static readonly DependencyProperty IsContentUnreadProperty =
            DependencyProperty.Register("IsContentUnread", typeof(bool), typeof(DiceView), new PropertyMetadata(false));

        #endregion

        public event EventHandler<int> IndexChanged;

        public event EventHandler FirstFrameRendered;
    }
}
