//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Controls.Chats;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Collections
{
    public partial class SearchStickersCollection : IncrementalCollection<object>, IAutocompleteCollection
    {
        enum Phase
        {
            None,
            GetStickers,
            SearchStickers
        }

        private readonly IClientService _clientService;
        private readonly StickerType _type;
        private readonly string _query;
        private readonly long _chatId;

        private Phase _phase = Phase.GetStickers;

        private readonly HashSet<int> _ids;

        public bool IsCustomEmoji => _type is StickerTypeCustomEmoji;

        public SearchStickersCollection(IClientService clientService, bool customEmoji, string query, long chatId)
        {
            _clientService = clientService;
            _type = customEmoji ? new StickerTypeCustomEmoji() : new StickerTypeRegular();
            _query = query;
            _chatId = chatId;

            _ids = new HashSet<int>();
            _phase = AppSettings.Stickers.SuggestionMode != StickersSuggestionMode.None
                ? Phase.GetStickers
                : AppSettings.Stickers.SuggestionMode == StickersSuggestionMode.All && _type is not StickerTypeCustomEmoji
                ? Phase.SearchStickers
                : Phase.None;
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            if (_phase == Phase.GetStickers)
            {
                _phase = AppSettings.Stickers.SuggestionMode == StickersSuggestionMode.All && _type is not StickerTypeCustomEmoji
                    ? Phase.SearchStickers
                    : Phase.None;

                var response = await _clientService.SendAsync(new GetStickers(_type, _query, 1000, _chatId));
                if (response is Stickers stickers)
                {
                    foreach (var sticker in stickers.StickersValue)
                    {
                        _ids.Add(sticker.StickerValue.Id);

                        Add(sticker);
                        totalCount++;
                    }
                }
            }
            else if (_phase == Phase.SearchStickers)
            {
                _phase = Phase.None;

                var response = await _clientService.SendAsync(new SearchStickers(_type, _query, string.Empty, Array.Empty<string>(), 0, 20));
                if (response is Stickers stickers)
                {
                    foreach (var sticker in stickers.StickersValue)
                    {
                        if (_ids.Contains(sticker.StickerValue.Id))
                        {
                            continue;
                        }

                        Add(sticker);
                        totalCount++;
                    }
                }
            }

            return new IncrementalLoadResult(totalCount, _phase != Phase.None);
        }

        public string Query => _query;

        public Orientation Orientation => Orientation.Horizontal;

        public bool InsertOnKeyDown => false;
    }
}
