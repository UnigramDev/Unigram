//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;

namespace Telegram.Collections
{
    /// <summary>
    /// The emoji panel search, in the order Telegram Desktop and Android both present it: the
    /// plain emoji the query resolves to, then the custom emoji already installed, then what the
    /// server finds.
    /// </summary>
    public partial class SearchEmojiCollection : IncrementalCollection<object>
    {
        enum Phase
        {
            Local,
            Cloud,
            None
        }

        // What one page of the cloud search asks for. It is part of the key TDLib caches the
        // result under, so it has to be the same on every page or none of them ever hit.
        private const int Limit = 100;

        private readonly IClientService _clientService;
        private readonly EmojiDrawerMode _mode;
        private readonly long _chatId;
        private readonly string _inputLanguage;

        // The two halves the server's sticker search takes side by side: the text as typed, and
        // the emoji it resolves to. A category, or a query that is itself an emoji, has no text
        // half — the server would read a string of emoji as a name to match and find nothing.
        private readonly string _query;
        private string _emojis;

        private Phase _phase = Phase.Local;
        private int _offset;

        private readonly HashSet<int> _ids = new();

        public SearchEmojiCollection(IClientService clientService, string query, EmojiDrawerMode mode, long chatId)
        {
            _clientService = clientService;
            _mode = mode;
            _chatId = chatId;
            _inputLanguage = NativeUtils.GetKeyboardCulture();

            // A typed emoji is looked up as an emoji, not as text.
            _emojis = Emoji.ContainsSingleEmoji(query) ? query : string.Empty;
            _query = _emojis.Length > 0 ? string.Empty : query;
        }

        public SearchEmojiCollection(IClientService clientService, IEnumerable<string> emojis, EmojiDrawerMode mode, long chatId)
        {
            _clientService = clientService;
            _mode = mode;
            _chatId = chatId;
            _inputLanguage = NativeUtils.GetKeyboardCulture();

            _query = string.Empty;
            _emojis = string.Join(" ", emojis);
        }

        // Only the chat panel shows plain emoji; every other mode picks a custom emoji and nothing
        // else, so there a non-premium user is shown the ones they can't send rather than nothing.
        private bool AllowCustomEmoji => _mode != EmojiDrawerMode.Chat || _clientService.IsPremium;

        /// <summary>
        /// Runs the passes the result is judged on — what the query resolves to and what is
        /// already installed, then the first page from the server. Whatever is left the list
        /// pages in on its own as it scrolls.
        /// </summary>
        public async Task SearchAsync()
        {
            await LoadMoreItemsAsync(0);

            if (HasMoreItems)
            {
                await LoadMoreItemsAsync(0);
            }
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            if (_phase == Phase.Local)
            {
                _phase = Phase.Cloud;

                totalCount += await LoadLocalAsync();
            }
            else if (_phase == Phase.Cloud)
            {
                totalCount += await LoadCloudAsync();
            }

            return new IncrementalLoadResult(totalCount, _phase != Phase.None);
        }

        private async Task<uint> LoadLocalAsync()
        {
            var totalCount = 0u;
            var emojis = _emojis.Length > 0
                ? _emojis.Split(' ')
                : null;

            if (_query.Length > 0)
            {
                var response = await _clientService.SendAsync(new SearchEmojis(_query, new[] { _inputLanguage }));
                if (response is EmojiKeywords suggestions)
                {
                    emojis = suggestions.EmojiKeywordsValue
                        .Select(x => x.Emoji)
                        .Distinct()
                        .ToArray();

                    _emojis = string.Join(" ", emojis);
                }
            }

            if (emojis != null && _mode == EmojiDrawerMode.Chat)
            {
                foreach (var emoji in emojis)
                {
                    Add(ToEmojiData(emoji));
                    totalCount++;
                }
            }

            if (AllowCustomEmoji)
            {
                // Installed, featured and recently used custom emoji, which the cloud search below
                // doesn't return. By the resolved emoji rather than the text: get_stickers drops
                // the whole emoji list if any word in the query isn't an emoji, and matching
                // per-sticker keywords alone finds far less. The text is still worth one attempt
                // when nothing resolved, since those keywords are all there is to go on.
                var query = _emojis.Length > 0 ? _emojis : _query;

                var response = await _clientService.SendAsync(new GetStickers(new StickerTypeCustomEmoji(), query, Limit, _chatId));
                if (response is Stickers stickers)
                {
                    totalCount += AddStickers(stickers);
                }
            }

            return totalCount;
        }

        private async Task<uint> LoadCloudAsync()
        {
            if (!AllowCustomEmoji || (_emojis.Length == 0 && _query.Length == 0))
            {
                _phase = Phase.None;
                return 0;
            }

            var response = await _clientService.SendAsync(new SearchStickers(new StickerTypeCustomEmoji(), _emojis, _query, new[] { _inputLanguage }, _offset, Limit));
            if (response is Stickers stickers)
            {
                _offset += stickers.StickersValue.Count;

                // td_api has no next offset to hand back, so a short page is the end of it.
                if (stickers.StickersValue.Count < Limit)
                {
                    _phase = Phase.None;
                }

                return AddStickers(stickers);
            }

            _phase = Phase.None;
            return 0;
        }

        private uint AddStickers(Stickers stickers)
        {
            var totalCount = 0u;

            foreach (var sticker in stickers.StickersValue)
            {
                if (_ids.Add(sticker.StickerValue.Id))
                {
                    Add(new StickerViewModel(_clientService, sticker));
                    totalCount++;
                }
            }

            return totalCount;
        }

        private static object ToEmojiData(string emoji)
        {
            if (Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji) || Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji.TrimEnd('\uFE0F')))
            {
                return AppSettings.Emoji.GetEmojiSkinTone(emoji);
            }

            return new EmojiData(emoji);
        }
    }
}
