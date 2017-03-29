﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public abstract class SettingsStickersArchivedViewModelBase : UnigramViewModelBase
    {
        private readonly IStickersService _stickersService;
        private readonly StickerType _type;

        public SettingsStickersArchivedViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService, StickerType type)
            : base(protoService, cacheService, aggregator)
        {
            _type = type;
            _stickersService = stickersService;
            _stickersService.NeedReloadArchivedStickers += OnNeedReloadArchivedStickers;

            Items = new SettingsStickersArchivedCollection(protoService, type);
        }

        private void OnNeedReloadArchivedStickers(object sender, NeedReloadArchivedStickersEventArgs e)
        {
            if (e.Type == _type)
            {
                Execute.BeginOnUIThread(() =>
                {
                    Items.HasMoreItems = false;
                    Items.Clear();
                    Items = new SettingsStickersArchivedCollection(ProtoService, _type);

                    RaisePropertyChanged(() => Items);
                });
            }
        }

        public SettingsStickersArchivedCollection Items { get; private set; }

        public class SettingsStickersArchivedCollection : IncrementalCollection<TLMessagesStickerSet>
        {
            private readonly IMTProtoService _protoService;
            private readonly StickerType _type;

            public SettingsStickersArchivedCollection(IMTProtoService protoService, StickerType type)
            {
                _protoService = protoService;
                _type = type;
            }

            public override async Task<IEnumerable<TLMessagesStickerSet>> LoadDataAsync()
            {
                var offset = Count == 0 ? 0 : this[Count - 1].Set.Id;
                var limit = 15;
                var masks = _type == StickerType.Mask;
                var response = await _protoService.GetArchivedStickersAsync(offset, limit, masks);
                if (response.IsSucceeded)
                {
                    return response.Result.Sets.Select(x =>
                    {
                        if (x is TLStickerSetMultiCovered multi)
                        {
                            return new TLMessagesStickerSet
                            {
                                Set = multi.Set,
                                Documents = multi.Covers
                            };
                        }
                        else if (x is TLStickerSetCovered single)
                        {
                            return new TLMessagesStickerSet
                            {
                                Set = single.Set,
                                Documents = new TLVector<TLDocumentBase> { single.Cover }
                            };
                        }

                        return null;
                    });
                }

                return new TLMessagesStickerSet[0];
            }
        }
    }
}
