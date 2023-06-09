//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

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
    }

    public struct EmojiPosition
    {
        public long CustomEmojiId { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
