using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI;

namespace Unigram.Controls.Messages
{
    public class CustomEmojiCanvas : AnimatedControl<object>
    {
        private readonly HashSet<long> _cache = new();
        private readonly List<EmojiPosition> _positions = new();

        private int _emojiSize;

        private bool _dropState;

        public CustomEmojiCanvas()
            : base(true)
        {
            DefaultStyleKey = typeof(CustomEmojiCanvas);
            AutoPlay = false;

            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            _emojiSize = GetDpiAwareSize(18);
        }

        protected override CanvasBitmap CreateBitmap(ICanvasResourceCreator sender)
        {
            var awareSize = GetDpiAwareSize(_currentSize.ToSize());

            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != awareSize.Width || _bitmap?.Size.Height != awareSize.Height;

            if (needsCreate && _animation != null)
            {
                _emojiSize = GetDpiAwareSize(18);
                return CreateBitmap(sender, awareSize.Width, awareSize.Height);
            }

            return needsCreate ? null : _bitmap;
        }

        protected override void Dispose()
        {
            var hash = GetHashCode();

            foreach (var customEmojiId in _cache)
            {
                EmojiRendererCache.Release(customEmojiId, hash);
            }

            _cache.Clear();
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            ICanvasBrush brush = null;
            ISet<long> outdated = null;

            if (_dropState)
            {
                var buffer = ArrayPool<byte>.Shared.Rent((int)(_bitmap.SizeInPixels.Width * _bitmap.SizeInPixels.Height * 4));

                _dropState = false;
                _bitmap.SetPixelBytes(buffer);

                ArrayPool<byte>.Shared.Return(buffer);
            }

            foreach (var item in _positions)
            {
                if (EmojiRendererCache.TryGet(item.CustomEmojiId, out EmojiRenderer animation))
                {
                    var x = (int)(item.X * (_currentDpi / 96));
                    var y = (int)(item.Y * (_currentDpi / 96));

                    var matches = _emojiSize * _emojiSize * 4 == animation.Buffer?.Length;
                    if (matches && animation.HasRenderedFirstFrame && x + _emojiSize < _bitmap.Size.Width && y + _emojiSize < _bitmap.Size.Height)
                    {
                        _bitmap.SetPixelBytes(animation.Buffer, x, y, _emojiSize, _emojiSize);
                        animation.CloseOutline();
                    }
                    else
                    {
                        args.FillGeometry(animation.GetOutline(sender), item.X, item.Y, brush ??= new CanvasSolidColorBrush(sender, Color.FromArgb(0x33, 0x7A, 0x8A, 0x96)));

                        if (_subscribed && !matches && !animation.IsLoading)
                        {
                            outdated ??= new HashSet<long>();
                            outdated.Add(item.CustomEmojiId);
                        }
                    }
                }
            }

            args.DrawImage(_bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));

            if (brush != null)
            {
                brush.Dispose();
            }

            if (outdated != null)
            {
                EmojiRendererCache.Reload(outdated, _emojiSize);
            }
        }

        protected override void NextFrame()
        {
            //foreach (var item in _cache.Values)
            //{
            //    item.NextFrame();
            //}
        }

        protected override void OnSubscribe(bool subscribe)
        {
            if (subscribe == _subscribed)
            {
                return;
            }

            var hash = GetHashCode();

            foreach (var item in _cache)
            {
                if (subscribe)
                {
                    EmojiRendererCache.Show(item, hash);
                }
                else
                {
                    EmojiRendererCache.Hide(item, hash);
                }
            }
        }

        protected override void SourceChanged()
        {
            OnSourceChanged();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _layoutRoot?.Measure(availableSize);
            _canvas?.Measure(availableSize);

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _layoutRoot?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            _canvas?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            if (_canvas != null)
            {
                _canvas.MaxWidth = finalSize.Width;
                _canvas.MaxHeight = finalSize.Height;
            }

            return finalSize;
        }

        public void UpdatePositions(IList<EmojiPosition> positions)
        {
            _positions.Clear();
            _positions.AddRange(positions);

            _dropState = true;
        }

        public async void UpdateEntities(IProtoService protoService, ISet<long> customEmoji)
        {
            //if (_loadEmojiToken != null)
            //{
            //    _loadEmojiToken.Cancel();
            //}

            //var tsc = _loadEmoji = new TaskCompletionSource<bool>();
            //var token = _loadEmojiToken = new CancellationTokenSource();

            _animation = new object();

            var request = new List<long>();
            var hash = GetHashCode();

            foreach (var emoji in customEmoji)
            {
                if (_cache.Contains(emoji))
                {
                    continue;
                }

                if (EmojiRendererCache.TryMerge(emoji, hash, _subscribed, out _))
                {
                    _cache.Add(emoji);
                    continue;
                }

                request.Add(emoji);
            }

            if (request.Count < 1)
            {
                return;
            }

            var response = await protoService.SendAsync(new GetCustomEmojiStickers(request));
            if (response is Stickers stickers)
            {
                //if (token.IsCancellationRequested)
                //{
                //    return;
                //}

                foreach (var sticker in stickers.StickersValue)
                {
                    if (_cache.Contains(sticker.CustomEmojiId))
                    {
                        continue;
                    }

                    _cache.Add(sticker.CustomEmojiId);
                    EmojiRendererCache.MergeOrCreate(protoService, sticker, _emojiSize, hash, _subscribed);
                }
            }

            OnSourceChanged();
        }

        static class EmojiRendererCache
        {
            private static readonly ConcurrentDictionary<long, EmojiRenderer> _cache = new();

            private static readonly SemaphoreSlim _nextFrameLock = new(1, 1);
            private static readonly HashSet<long> _count = new();

            private static ThreadPoolTimer _timer;
            private static TimeSpan _interval;

            private static readonly object _lock = new();

            static EmojiRendererCache()
            {
                _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            }

            public static bool Contains(long customEmojiId)
            {
                return _cache.ContainsKey(customEmojiId);
            }

            public static bool TryGet(long customEmojiId, out EmojiRenderer renderer)
            {
                return _cache.TryGetValue(customEmojiId, out renderer);
            }

            public static bool TryMerge(long customEmojiId, int hash, bool subscribe, out EmojiRenderer renderer)
            {
                if (_cache.TryGetValue(customEmojiId, out renderer))
                {
                    if (subscribe)
                    {
                        Show(renderer, hash);
                    }

                    lock (_lock)
                    {
                        renderer.References.Add(hash);
                    }

                    return true;
                }

                return false;
            }

            public static void MergeOrCreate(IProtoService protoService, Sticker sticker, int size, int hash, bool subscribe)
            {
                if (TryMerge(sticker.CustomEmojiId, hash, subscribe, out EmojiRenderer renderer))
                {
                    return;
                }

                _cache[sticker.CustomEmojiId] = renderer = new EmojiRenderer(protoService, sticker, size);

                if (subscribe)
                {
                    Show(renderer, hash);
                }

                lock (_lock)
                {
                    renderer.References.Add(hash);
                }
            }

            public static void Release(long customEmojiId, int hash)
            {
                if (_cache.TryGetValue(customEmojiId, out EmojiRenderer reference))
                {
                    lock (_lock)
                    {
                        reference.References.Remove(hash);
                        reference.HashCodes.Remove(hash);
                    }

                    if (reference.IsAlive is false)
                    {
                        _count.Remove(customEmojiId);
                        _cache.TryRemove(customEmojiId, out _);

                        reference.Dispose();
                    }
                }
            }

            public static void Show(long customEmojiId, int hash)
            {
                if (TryGet(customEmojiId, out EmojiRenderer renderer))
                {
                    Show(renderer, hash);
                }
            }

            public static void Show(EmojiRenderer renderer, int hash)
            {
                lock (_lock)
                {
                    renderer.HashCodes.Add(hash);

                    if (renderer.IsVisible)
                    {
                        _count.Add(renderer.CustomEmojiId);
                    }

                    if (_count.Count > 0 && _timer == null)
                    {
                        _timer = ThreadPoolTimer.CreatePeriodicTimer(PrepareNextFrame, _interval);
                    }
                }
            }

            public static void Hide(long customEmojiId, int hash)
            {
                if (TryGet(customEmojiId, out EmojiRenderer renderer))
                {
                    Hide(renderer, hash);
                }
            }

            public static void Hide(EmojiRenderer renderer, int hash)
            {
                lock (_lock)
                {
                    renderer.HashCodes.Remove(hash);

                    if (renderer.IsVisible is false)
                    {
                        _count.Remove(renderer.CustomEmojiId);
                    }

                    if (_count.Count == 0 && _timer != null)
                    {
                        _timer.Cancel();
                        _timer = null;
                    }
                }
            }

            private static void PrepareNextFrame(ThreadPoolTimer timer)
            {
                if (_nextFrameLock.Wait(0))
                {
                    NextFrame();
                    _nextFrameLock.Release();
                }
            }

            private static void NextFrame()
            {
                foreach (var item in _cache.Values)
                {
                    if (item.IsVisible)
                    {
                        item.NextFrame();
                    }
                }
            }

            public static void Reload(ISet<long> emoji, int size)
            {
                List<EmojiRenderer> renderers = null;

                foreach (var customEmojiId in emoji)
                {
                    if (_cache.TryGetValue(customEmojiId, out EmojiRenderer renderer))
                    {
                        if (renderer.Size != size)
                        {
                            var clone = renderer.Clone(size);
                            renderer.Dispose();

                            _cache[customEmojiId] = clone;
                            clone.IsLoading = true;

                            renderers ??= new List<EmojiRenderer>();
                            renderers.Add(clone);
                        }
                        else if (renderer.Buffer == null && !renderer.IsLoading)
                        {
                            renderer.IsLoading = true;

                            renderers ??= new List<EmojiRenderer>();
                            renderers.Add(renderer);
                        }
                    }
                }

                _ = Windows.System.Threading.ThreadPool.RunAsync(async _ =>
                {
                    foreach (var item in renderers)
                    {
                        if (item.Buffer == null)
                        {
                            await item.CreateAsync();
                            await Task.Delay(10);
                        }
                    }
                }, WorkItemPriority.Low);
            }
        }

        class EmojiRenderer
        {
            private readonly IProtoService _protoService;
            private readonly Sticker _sticker;

            private object _animation;
            private IBuffer _buffer;

            private int _size;

            private int _animationTotalFrame;
            private double _animationFrameRate;

            private bool _limitFps = true;
            private int _index;

            public EmojiRenderer(IProtoService protoService, Sticker sticker, int size)
            {
                _protoService = protoService;
                _sticker = sticker;
                _size = size;
            }

            public EmojiRenderer Clone(int size)
            {
                return new EmojiRenderer(_protoService, _sticker, size);
            }

            public long CustomEmojiId => _sticker.CustomEmojiId;

            public IBuffer Buffer => _buffer;

            public int Size => _size;

            public HashSet<int> References { get; } = new();
            public HashSet<int> HashCodes { get; } = new();

            public bool IsAlive => References.Count > 0;
            public bool IsVisible => HashCodes.Count > 0;

            public bool IsLoading { get; set; }

            private CanvasGeometry _outline;
            public CanvasGeometry GetOutline(ICanvasResourceCreator sender)
            {
                if (_outline == null || _outline.Device != sender.Device)
                {
                    var scale = 18f / _sticker.Width;
                    var outline = CompositionPathParser.Parse(sender, _sticker.Outline, scale);

                    _outline = outline;
                }

                return _outline;
            }

            public void CloseOutline()
            {
                if (_outline != null)
                {
                    _outline.Dispose();
                    _outline = null;
                }
            }

            private bool _hasRenderedFirstFrame;
            public bool HasRenderedFirstFrame => _hasRenderedFirstFrame && _buffer != null;

            public async Task CreateAsync()
            {
                var file = _sticker.StickerValue;
                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    await _protoService.SendAsync(new DownloadFile(file.Id, 32, 0, 0, true));
                }

                if (file.Local.IsFileExisting())
                {
                    if (_sticker.Format is StickerFormatTgs)
                    {
                        var animation = LottieAnimation.LoadFromFile(file.Local.Path, new Windows.Graphics.SizeInt32 { Width = _size, Height = _size }, true, null);
                        if (animation != null)
                        {
                            _animationTotalFrame = animation.TotalFrame;
                            _animationFrameRate = animation.FrameRate;

                            _buffer = BufferSurface.Create((uint)(_size * _size * 4));
                            _animation = animation;
                        }
                    }
                }
            }

            public void NextFrame()
            {
                var animation = _animation as LottieAnimation;
                if (animation == null || animation.IsCaching || _buffer == null /*|| _unloaded*/)
                {
                    return;
                }

                var index = _index;
                var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

                animation.RenderSync(_buffer, _size, _size, index);

                if (index + framesPerUpdate < _animationTotalFrame)
                {
                    _index += framesPerUpdate;
                }
                else
                {
                    _index = 0;
                }

                _hasRenderedFirstFrame = true;
            }

            public void Dispose()
            {
                if (_animation is LottieAnimation lottie && !lottie.IsCaching)
                {
                    lottie.Dispose();
                }

                _buffer = null;
            }
        }
    }

    public struct EmojiPosition
    {
        public long CustomEmojiId { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }

}
