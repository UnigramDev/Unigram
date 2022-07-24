using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Threading;
using Windows.UI;

namespace Unigram.Controls.Messages
{
    public class CustomEmojiCanvas : AnimatedControl<object>
    {
        private readonly ConcurrentDictionary<long, StickerAdapter> _cache = new();
        private readonly List<StickerPosition> _positions = new();

        private DispatcherQueueController _queue;
        private bool _dropState;

        public CustomEmojiCanvas()
            : base(true)
        {
            DefaultStyleKey = typeof(CustomEmojiCanvas);
            AutoPlay = false;

            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
        }

        public void UpdatePositions(IList<StickerPosition> positions)
        {
            _positions.Clear();
            _positions.AddRange(positions);

            _dropState = true;
        }

        protected override CanvasBitmap CreateBitmap(ICanvasResourceCreator sender)
        {
            var width = (int)_currentSize.X;
            var height = (int)_currentSize.Y;

            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != width || _bitmap?.Size.Height != height;

            if (needsCreate && _animation != null)
            {
                return CreateBitmap(sender, width, height);
            }

            return needsCreate ? null : _bitmap;
        }

        protected override void Dispose()
        {
            foreach (var animation in _cache.Values)
            {
                animation.Dispose();
            }

            _cache.Clear();
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            ICanvasBrush brush = null;

            if (_dropState)
            {
                var buffer = ArrayPool<byte>.Shared.Rent((int)(_bitmap.SizeInPixels.Width * _bitmap.SizeInPixels.Height * 4));

                _dropState = false;
                _bitmap.SetPixelBytes(buffer);

                ArrayPool<byte>.Shared.Return(buffer);
            }

            foreach (var item in _positions)
            {
                if (_cache.TryGetValue(item.CustomEmojiId, out StickerAdapter animation))
                {
                    if (animation.HasRenderedFirstFrame && item.X + 18 < _bitmap.Size.Width && item.Y + 18 < _bitmap.Size.Height)
                    {
                        _bitmap.SetPixelBytes(animation.Buffer, item.X, item.Y, 18, 18);
                        animation.CloseOutline();
                    }
                    else
                    {
                        args.FillGeometry(animation.GetOutline(sender), item.X, item.Y, brush ??= new CanvasSolidColorBrush(sender, Colors.Black));
                    }
                }
            }

            args.DrawImage(_bitmap);

            if (brush != null)
            {
                brush.Dispose();
            }
        }

        protected override void NextFrame()
        {
            foreach (var item in _cache.Values)
            {
                item.NextFrame();
            }
        }

        //protected override void OnSubscribe(bool subscribe)
        //{
        //    foreach (var item in _cache.Values)
        //    {
        //        item?.Subscribe(subscribe);
        //    }
        //}

        protected override void SourceChanged()
        {
            OnSourceChanged();
        }

        private MessageViewModel _message;

        public async void UpdateEntities(MessageViewModel message, IList<long> customEmoji)
        {
            //if (_loadEmojiToken != null)
            //{
            //    _loadEmojiToken.Cancel();
            //}

            //var tsc = _loadEmoji = new TaskCompletionSource<bool>();
            //var token = _loadEmojiToken = new CancellationTokenSource();

            _animation = new object();
            _message = message;

            var request = new List<long>();

            foreach (var emoji in customEmoji)
            {
                if (request.Contains(emoji) || _cache.ContainsKey(emoji))
                {
                    continue;
                }

                request.Add(emoji);
            }

            if (request.Count < 1)
            {
                return;
            }

            var response = await message.ProtoService.SendAsync(new GetCustomEmojiStickers(request));
            if (response is Stickers stickers)
            {
                //if (token.IsCancellationRequested)
                //{
                //    return;
                //}

                foreach (var sticker in stickers.StickersValue)
                {
                    if (_cache.ContainsKey(sticker.CustomEmojiId))
                    {
                        continue;
                    }

                    _cache[sticker.CustomEmojiId] = new StickerAdapter(sticker);
                }
            }

            OnSourceChanged();
        }

        protected override void OnPlay()
        {
            foreach (var animation in _cache.Values)
            {
                if (animation.Buffer == null)
                {
                    _queue ??= DispatcherQueueController.CreateOnDedicatedThread();
                    _queue.DispatcherQueue.TryEnqueue(() => LoadSticker(_message.ProtoService, animation));
                }
            }
        }

        private async void LoadSticker(IProtoService protoService, StickerAdapter animation)
        {
            await animation.CreateAsync(protoService);
        }

        class StickerAdapter
        {
            private readonly Sticker _sticker;

            private object _animation;
            private IBuffer _buffer;

            protected readonly SemaphoreSlim _nextFrameLock = new(1, 1);

            private ThreadPoolTimer _timer;

            private int _animationTotalFrame;
            private double _animationFrameRate;

            private bool _limitFps = true;
            private int _index;

            private TimeSpan _interval;

            public StickerAdapter(Sticker sticker)
            {
                _sticker = sticker;
                _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            }

            public IBuffer Buffer => _buffer;

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

            public int Width => _sticker.Width;

            public bool HasRenderedFirstFrame { get; private set; }

            public async Task CreateAsync(IProtoService protoService)
            {
                var file = _sticker.StickerValue;
                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    await protoService.SendAsync(new DownloadFile(file.Id, 32, 0, 0, true));
                }

                if (file.Local.IsFileExisting())
                {
                    if (_sticker.Format is StickerFormatTgs)
                    {
                        var animation = LottieAnimation.LoadFromFile(file.Local.Path, new Windows.Graphics.SizeInt32 { Width = 18, Height = 18 }, true, null);
                        if (animation != null)
                        {
                            _animationTotalFrame = animation.TotalFrame;
                            _animationFrameRate = animation.FrameRate;

                            _buffer = BufferSurface.Create(18 * 18 * 4);
                            _animation = animation;
                        }
                    }
                }
            }

            public void Subscribe(bool subscribe)
            {
                if (subscribe)
                {
                    _timer ??= ThreadPoolTimer.CreatePeriodicTimer(PrepareNextFrame, _interval);
                }
                else
                {
                    if (_timer != null)
                    {
                        _timer.Cancel();
                        _timer = null;
                    }
                }
            }

            private void PrepareNextFrame(ThreadPoolTimer timer)
            {
                //lock (_nextFrameLock)
                _nextFrameLock.Wait();
                {
                    NextFrame();
                }
                _nextFrameLock.Release();
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

                animation.RenderSync(_buffer, 18, 18, index);

                if (index + framesPerUpdate < _animationTotalFrame)
                {
                    _index += framesPerUpdate;
                }
                else
                {
                    _index = 0;
                }

                HasRenderedFirstFrame = true;
            }

            public void Dispose()
            {
                if (_animation is LottieAnimation lottie && !lottie.IsCaching)
                {
                    lottie.Dispose();
                }
            }
        }
    }

    public struct StickerPosition
    {
        public long CustomEmojiId { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }

}
