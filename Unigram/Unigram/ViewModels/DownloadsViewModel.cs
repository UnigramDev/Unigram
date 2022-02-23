using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class DownloadsViewModel : TLViewModelBase
    {
        public DownloadsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemCollection(protoService, string.Empty, false, false);
        }

        public ItemCollection Items { get; private set; }

        public class ItemCollection : ObservableCollection<FileDownload>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;

            private readonly string _query;
            private readonly bool _onlyActive;
            private readonly bool _onlyCompleted;

            private string _offset = string.Empty;
            private bool _hasMoreItems = true;
            
            public ItemCollection(IProtoService protoService, string query, bool onlyActive, bool onlyCompleted)
            {
                _protoService = protoService;

                _query = query;
                _onlyActive = onlyActive;
                _onlyCompleted = onlyCompleted;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _protoService.SendAsync(new SearchFileDownloads(_query, _onlyActive, _onlyCompleted, _offset, 100));
                    if (response is FoundFileDownloads found)
                    {
                        foreach (var item in found.Files)
                        {
                            Add(item);
                        }

                        _offset = found.NextOffset;
                        _hasMoreItems = found.NextOffset.Length > 0;

                        return new LoadMoreItemsResult { Count = (uint)found.Files.Count };
                    }

                    _offset = string.Empty;
                    _hasMoreItems = false;

                    return new LoadMoreItemsResult { Count = 0 };
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }
}
