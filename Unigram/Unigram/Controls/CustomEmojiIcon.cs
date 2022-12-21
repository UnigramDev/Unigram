using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Controls.Messages;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class CustomEmojiIcon : AnimatedControl<object>
    {
        private Sticker _sticker;
        private long? _cache;

        private IClientService _clientService;

        private EmojiRendererCache _pool;
        private int _emojiSize;

        private int _loopCount = -1;
        private int _loopTrack;

        private bool _withinViewport;

        public CustomEmojiIcon()
            : base(true)
        {
            DefaultStyleKey = typeof(CustomEmojiIcon);

            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            _emojiSize = GetDpiAwareSize(_frameSize);
            _pool = EmojiCache.MergeOrCreate(_emojiSize, GetHashCode());

            EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        //protected override void OnAutoPlayChanged(bool newValue, bool oldValue)
        //{
        //    if (newValue)
        //    {
        //        EffectiveViewportChanged += OnEffectiveViewportChanged;
        //    }
        //    else
        //    {
        //        EffectiveViewportChanged -= OnEffectiveViewportChanged;
        //    }
        //}

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var within = args.BringIntoViewDistanceX == 0 || args.BringIntoViewDistanceY == 0;
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

        protected override void OnDpiChanged(float currentDpi)
        {
            var hash = GetHashCode();

            if (_cache is long customEmojiId)
            {
                _pool.Release(customEmojiId, hash);
            }

            EmojiCache.Release(_emojiSize, hash);

            _emojiSize = GetDpiAwareSize(_frameSize);
            _pool = EmojiCache.MergeOrCreate(_emojiSize, hash);

            if (_clientService != null)
            {
                if (_sticker != null)
                {
                    SetSticker(_clientService, _sticker);
                }
                else if (_cache != null)
                {
                    SetCustomEmoji(_clientService, _cache.Value);
                }
            }
        }

        protected override CanvasBitmap CreateBitmap(CanvasDevice device)
        {
            var awareSize = GetDpiAwareSize(_frameSize);

            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != awareSize || _bitmap?.Size.Height != awareSize;
            needsCreate |= _bitmap?.Device != device;
            needsCreate &= _currentSize.X > 0 && _currentSize.Y > 0;

            if (needsCreate)
            {
                _emojiSize = GetDpiAwareSize(_frameSize);
                return CreateBitmap(device, awareSize, awareSize);
            }

            return null;
        }

        protected override void Dispose()
        {
            if (_cache is long customEmojiId)
            {
                _pool.Release(customEmojiId, GetHashCode());
            }

            _sticker = null;
            _cache = null;
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            ICanvasBrush brush = null;
            ISet<long> outdated = null;

            if (_cache is long item && _pool.TryGet(item, out EmojiRenderer animation))
            {
                var matches = animation.PixelWidth * animation.PixelHeight * 4 == animation.Buffer?.Length;
                if (matches && animation.HasRenderedFirstFrame && _emojiSize == _bitmap.Size.Width && _emojiSize == _bitmap.Size.Height)
                {
                    _bitmap.SetPixelBytes(animation.Buffer, 0, 0, animation.PixelWidth, animation.PixelHeight);
                    animation.CloseOutline();

                    if (animation.LoopCount != _loopTrack)
                    {
                        _loopCount++;
                        _loopTrack = animation.LoopCount;
                    }

                    var offsetX = (sender.SizeInPixels.Width - animation.PixelWidth) * (sender.Size.Width / sender.SizeInPixels.Width);
                    var offsetY = (sender.SizeInPixels.Height - animation.PixelHeight) * (sender.Size.Width / sender.SizeInPixels.Width);

                    args.DrawImage(_bitmap, new Rect(offsetX, offsetY, sender.Size.Width, sender.Size.Height));
                }
                else
                {
                    args.FillGeometry(animation.GetOutline(sender), 0, 0, brush ??= new CanvasSolidColorBrush(sender, Color.FromArgb(0x33, 0x7A, 0x8A, 0x96)));

                    if (_subscribed && !animation.IsLoading)
                    {
                        outdated ??= new HashSet<long>();
                        outdated.Add(item);
                    }
                }
            }

            if (brush != null)
            {
                brush.Dispose();
            }

            if (_loopCount == 2 && !_isLoopingEnabled)
            {
                Pause();
            }
            else if (outdated != null)
            {
                _pool.Reload(outdated);
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
            if (subscribe == _subscribed || _cache is not long item)
            {
                return;
            }

            if (subscribe && (_loopCount < 2 || _isLoopingEnabled))
            {
                _pool.Show(item, GetHashCode());
            }
            else
            {
                _pool.Hide(item, GetHashCode());
            }
        }

        protected override void SourceChanged()
        {
            OnSourceChanged();
        }

        public async void SetCustomEmoji(IClientService clientService, long emoji)
        {
            //if (_loadEmojiToken != null)
            //{
            //    _loadEmojiToken.Cancel();
            //}

            //var tsc = _loadEmoji = new TaskCompletionSource<bool>();
            //var token = _loadEmojiToken = new CancellationTokenSource();

            _clientService = clientService;
            _animation = new object();
            _loopCount = -1;

            if (_playing == false)
            {
                _playing = null;
            }

            var request = new List<long>();
            var hash = GetHashCode();

            if (_pool.TryMerge(clientService, emoji, hash, _subscribed, out _))
            {
                _cache = emoji;
                OnSourceChanged();
                return;
            }

            request.Add(emoji);

            if (request.Count < 1)
            {
                OnSourceChanged();
                return;
            }

            var response = await clientService.SendAsync(new GetCustomEmojiStickers(request));
            if (response is Stickers stickers && stickers.StickersValue.Count == 1)
            {
                var sticker = stickers.StickersValue[0];
                if (sticker.FullType is not StickerTypeFullInfoCustomEmoji customEmoji || customEmoji.CustomEmojiId == _cache)
                {
                    return;
                }

                _cache = customEmoji.CustomEmojiId;
                _pool.MergeOrCreate(clientService, sticker, hash, _subscribed);
            }

            OnSourceChanged();
        }

        public void SetReaction(IClientService clientService, EmojiReaction reaction)
        {
            SetSticker(clientService, reaction.CenterAnimation);
        }

        public void SetSticker(IClientService clientService, Sticker sticker)
        {
            //if (_loadEmojiToken != null)
            //{
            //    _loadEmojiToken.Cancel();
            //}

            //var tsc = _loadEmoji = new TaskCompletionSource<bool>();
            //var token = _loadEmojiToken = new CancellationTokenSource();

            _clientService = clientService;
            _animation = new object();
            _loopCount = -1;

            if (_playing == false)
            {
                _playing = null;
            }

            var hash = GetHashCode();
            var id = sticker.FullType is not StickerTypeFullInfoCustomEmoji customEmoji
                ? sticker.StickerValue.Id
                : customEmoji.CustomEmojiId;

            if (_pool.TryMerge(clientService, id, hash, _subscribed, out _))
            {
                _cache = id;
                OnSourceChanged();
                return;
            }

            _cache = id;
            _pool.MergeOrCreate(clientService, sticker, hash, _subscribed);

            OnSourceChanged();
        }

        public async void SetSticker(IClientService clientService, string emoji)
        {
            var response = await clientService.SendAsync(new GetEmojiReaction(emoji));
            if (response is not EmojiReaction reaction)
            {
                return;
            }

            SetReaction(clientService, reaction);
        }

        private int _frameSize = 20;
        public int FrameSize
        {
            get => _frameSize;
            set
            {
                if (_frameSize != value)
                {
                    _frameSize = value;
                    OnDpiChanged(_currentDpi);
                }
            }
        }
    }
}
