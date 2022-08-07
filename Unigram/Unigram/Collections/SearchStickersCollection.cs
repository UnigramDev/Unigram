using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Controls.Chats;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchStickersCollection : MvxObservableCollection<Sticker>, IAutocompleteCollection, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly ISettingsService _settings;
        private readonly StickerType _type;
        private readonly string _query;
        private readonly long _chatId;

        private bool _first = true;
        private bool _hasMore = true;

        private readonly HashSet<int> _ids;

        public bool IsCustomEmoji => _type is StickerTypeCustomEmoji;

        public SearchStickersCollection(IProtoService protoService, ISettingsService settings, bool customEmoji, string query, long chatId)
        {
            _protoService = protoService;
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

                    var response = await _protoService.SendAsync(new GetStickers(_type, _query, 1000, _chatId));
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

                    var response = await _protoService.SendAsync(new SearchStickers(_query, 20));
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
    }
}
