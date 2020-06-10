using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels.Settings
{
    public abstract class SettingsStickersArchivedViewModelBase : TLViewModelBase
    {
        public SettingsStickersArchivedViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, bool masks)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemsCollection(protoService, masks);
        }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : MvxObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly bool _masks;

            public ItemsCollection(IProtoService protoService, bool masks)
            {
                _protoService = protoService;
                _masks = masks;
            }

            //public override async Task<IList<TLMessagesStickerSet>> LoadDataAsync()
            //{
            //    var offset = Count == 0 ? 0 : this[Count - 1].Set.Id;
            //    var limit = 15;
            //    var masks = _type == StickerType.Mask;

            //    var response = await _protoService.GetArchivedStickersAsync(offset, limit, masks);
            //    if (response.IsSucceeded)
            //    {
            //        return response.Result.Sets.Select(x =>
            //        {
            //            if (x is TLStickerSetMultiCovered multi)
            //            {
            //                return new TLMessagesStickerSet
            //                {
            //                    Set = multi.Set,
            //                    Documents = multi.Covers
            //                };
            //            }
            //            else if (x is TLStickerSetCovered single)
            //            {
            //                return new TLMessagesStickerSet
            //                {
            //                    Set = single.Set,
            //                    Documents = new TLVector<TLDocumentBase> { single.Cover }
            //                };
            //            }

            //            return null;
            //        }).ToList();
            //    }

            //    return new TLMessagesStickerSet[0];
            //}

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var offset = 0L;

                    var last = this.LastOrDefault();
                    if (last != null)
                    {
                        offset = last.Id;
                    }

                    var response = await _protoService.SendAsync(new GetArchivedStickerSets(_masks, offset, 20));
                    if (response is StickerSets stickerSets)
                    {
                        foreach (var set in stickerSets.Sets)
                        {
                            Add(set);
                        }

                        return new LoadMoreItemsResult { Count = (uint)stickerSets.Sets.Count };
                    }

                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => true;
        }
    }
}
