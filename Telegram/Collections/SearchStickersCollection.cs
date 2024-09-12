//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Controls.Chats;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class SearchStickersCollection : MvxObservableCollection<object>, IAutocompleteCollection, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settings;
        private readonly StickerType _type;
        private readonly string _query;
        private readonly long _chatId;

        private bool _first = true;
        private bool _hasMore = true;

        private readonly HashSet<int> _ids;

        public bool IsCustomEmoji => _type is StickerTypeCustomEmoji;

        public SearchStickersCollection(IClientService clientService, ISettingsService settings, bool customEmoji, string query, long chatId)
        {
            _clientService = clientService;
            _settings = settings;
            _type = customEmoji ? new StickerTypeCustomEmoji() : new StickerTypeRegular();
            _query = query;
            _chatId = chatId;

            _ids = new HashSet<int>();
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = 0;

                if (_first && _settings.Stickers.SuggestionMode != StickersSuggestionMode.None)
                {
                    _first = false;

                    var response = await _clientService.SendAsync(new GetStickers(_type, _query, 1000, _chatId));
                    if (response is Stickers stickers)
                    {
                        foreach (var sticker in stickers.StickersValue)
                        {
                            _ids.Add(sticker.StickerValue.Id);

                            Add(sticker);
                            count++;
                        }
                    }
                }
                else if (!_first && _settings.Stickers.SuggestionMode == StickersSuggestionMode.All && _type is not StickerTypeCustomEmoji)
                {
                    _hasMore = false;

                    var response = await _clientService.SendAsync(new SearchStickers(_type, _query, 20));
                    if (response is Stickers stickers)
                    {
                        foreach (var sticker in stickers.StickersValue)
                        {
                            if (_ids.Contains(sticker.StickerValue.Id))
                            {
                                continue;
                            }

                            Add(sticker);
                            count++;
                        }
                    }
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => _hasMore;

        public string Query => _query;

        public Orientation Orientation => Orientation.Horizontal;

        public bool InsertOnKeyDown => false;
    }
}
