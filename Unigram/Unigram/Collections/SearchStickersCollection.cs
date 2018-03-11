using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchStickersCollection : MvxObservableCollection<Sticker>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly string _query;

        private bool _first = true;
        private bool _hasMore = true;

        public SearchStickersCollection(IProtoService protoService, string query)
        {
            _protoService = protoService;
            _query = query;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = 0;

                if (_first)
                {
                    _first = false;

                    var response = await _protoService.SendAsync(new GetStickers(_query, 1000));
                    if (response is Stickers stickers)
                    {
                        foreach (var sticker in stickers.StickersValue)
                        {
                            Add(sticker);
                            count++;
                        }
                    }
                }
                else
                {
                    _hasMore = false;

                    var response = await _protoService.SendAsync(new SearchStickers(_query, 20));
                    if (response is Stickers stickers)
                    {
                        foreach (var sticker in stickers.StickersValue)
                        {
                            Add(sticker);
                            count++;
                        }
                    }
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => _hasMore;
    }
}
