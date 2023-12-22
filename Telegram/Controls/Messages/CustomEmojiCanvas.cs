//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using RLottie;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public class CustomEmojiCanvas : Canvas
    {
        public void UpdateEntities(IClientService clientService, IList<EmojiPosition> positions)
        {
            if (positions == null || positions.Count == 0)
            {
                Children.Clear();
                return;
            }

            var max = Math.Max(positions.Count, Children.Count);

            for (int i = 0; i < max; i++)
            {
                if (i >= positions.Count)
                {
                    Children.RemoveAt(Children.Count - 1);
                }
                else
                {
                    var player = i >= Children.Count ? null : Children[i] as AnimatedImage;
                    if (player == null)
                    {
                        player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.IsHitTestVisible = false;

                        Children.Add(player);
                    }

                    player.Source = new CustomEmojiFileSource(clientService, positions[i].CustomEmojiId);

                    SetTop(player, positions[i].Y);
                    SetLeft(player, positions[i].X);
                }
            }
        }
    }

    public class EmojiCache
    {
        private static readonly ConcurrentDictionary<long, Sticker> _stickers = new();

        public static bool TryGet(long customEmojiId, out Sticker sticker)
        {
            return _stickers.TryGetValue(customEmojiId, out sticker);
        }

        public static void AddOrUpdate(Sticker sticker)
        {
            var id = sticker.FullType is not StickerFullTypeCustomEmoji customEmoji
                ? sticker.StickerValue.Id
                : customEmoji.CustomEmojiId;

            _stickers[id] = sticker;
        }

        public static async Task<IList<Sticker>> GetAsync(IClientService clientService, IList<long> customEmojiIds)
        {
            var response = await clientService.SendAsync(new GetCustomEmojiStickers(customEmojiIds));
            if (response is Stickers stickers)
            {
                foreach (var sticker in stickers.StickersValue)
                {
                    if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        _stickers[customEmoji.CustomEmojiId] = sticker;
                    }
                }

                return stickers.StickersValue;
            }

            return Array.Empty<Sticker>();
        }

        public static async Task<Sticker> GetAsync(IClientService clientService, long customEmojiId)
        {
            var response = await clientService.SendAsync(new GetCustomEmojiStickers(new[] { customEmojiId }));
            if (response is Stickers stickers)
            {
                foreach (var sticker in stickers.StickersValue)
                {
                    if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        _stickers[customEmoji.CustomEmojiId] = sticker;
                        return sticker;
                    }
                }
            }

            return null;
        }




        [ThreadStatic]
        private static Dictionary<GetLottieFrameOp, Task<ImageSource>> _operations;
        private static Dictionary<GetLottieFrameOp, Task<ImageSource>> Operations => _operations ??= new();

        private record GetLottieFrameOp(int SessionId, File File, int Width, int Height);

        public static Task<ImageSource> GetLottieFrameAsync(IClientService clientService, File file, int width, int height)
        {
            var dpi = WindowContext.Current.RasterizationScale;

            width = (int)(width * dpi);
            height = (int)(height * dpi);

            var op = new GetLottieFrameOp(clientService.SessionId, file, width, height);

            if (Operations.TryGetValue(op, out var task))
            {
                if (task.IsCompleted)
                {
                    Operations.Remove(op);
                }

                return task;
            }

            return Operations[op] = GetLottieFrame(op, clientService, file, 0, width, height);
        }

        private static async Task<ImageSource> GetLottieFrame(GetLottieFrameOp op, IClientService clientService, File file, int frame, int width, int height)
        {
            if (file.Local.IsDownloadingCompleted is false)
            {
                // TODO: if the download doesn't complete, this control will leak
                file = await clientService.DownloadFileAsync(file, 32);
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var path = file.Local.Path + $".{width}x{height}.static.cache";

                if (!NativeUtils.FileExists(path))
                {
                    await Task.Run(() =>
                    {
                        var animation = LottieAnimation.LoadFromFile(file.Local.Path, width, height, false, null);
                        if (animation != null)
                        {
                            animation.RenderSync(path, frame);
                            animation.Dispose();
                        }
                    });
                }

                Operations.Remove(op);
                return UriEx.ToBitmap(path);
            }

            Operations.Remove(op);
            return null;
        }
    }

    public struct EmojiPosition
    {
        public long CustomEmojiId { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
