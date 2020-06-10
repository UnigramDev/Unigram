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
        private readonly string _query;

        private bool _first = true;
        private bool _hasMore = true;

        private readonly List<int> _ids;

        public SearchStickersCollection(IProtoService protoService, ISettingsService settings, string query)
        {
            _protoService = protoService;
            _settings = settings;
            _query = query;

            _ids = new List<int>();
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = 0;

                if (_first && _settings.Stickers.SuggestionMode != StickersSuggestionMode.None)
                {
                    _first = false;

                    var response = await _protoService.SendAsync(new GetStickers(_query, 1000));
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
                else if (!_first && _settings.Stickers.SuggestionMode == StickersSuggestionMode.All)
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

        public Orientation Orientation => Orientation.Horizontal;
    }
}
