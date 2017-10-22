using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelEditStickerSetViewModel : UnigramViewModelBase, IHandle<StickersDidLoadedEventArgs>
    {
        private readonly IStickersService _stickersService;

        public ChannelEditStickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;

            SendCommand = new RelayCommand(SendExecute);
            CancelCommand = new RelayCommand(CancelExecute);

            Items = new MvxObservableCollection<TLMessagesStickerSet>();

            Aggregator.Subscribe(this);
        }

        private TLChannelFull _full;
        public TLChannelFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        private TLChannel _item;
        public TLChannel Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        private string _shortName;
        public string ShortName
        {
            get
            {
                return _shortName;
            }
            set
            {
                Set(ref _shortName, value);
            }
        }

        private TLMessagesStickerSet _selectedItem;
        public TLMessagesStickerSet SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (value == _selectedItem)
                {
                    return;
                }

                Set(ref _selectedItem, value);
                Set(() => ShortName, ref _shortName, value?.Set.ShortName);

                if (value != null && _stickersService.IsStickerPackInstalled(value.Set.Id))
                {
                    ListSelectedItem = Items.FirstOrDefault(x => x.Set.Id == value.Set.Id) ?? value;
                }
                else
                {
                    ListSelectedItem = null;
                }
            }
        }

        private TLMessagesStickerSet _listSelectedItem;
        public TLMessagesStickerSet ListSelectedItem
        {
            get
            {
                return _listSelectedItem;
            }
            set
            {
                if (value == _listSelectedItem)
                {
                    return;
                }

                Set(ref _listSelectedItem, value);

                if (value != null)
                {
                    SelectedItem = value;
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var stickers = _stickersService.CheckStickers(StickerType.Image);
                if (stickers) ProcessStickerSets(StickerType.Image);
            });

            Item = null;
            Full = null;
            SelectedItem = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full != null)
                {
                    Full = full;
                    SelectedItem = _stickersService.GetGroupStickerSetById(full.StickerSet);
                }
            }
        }

        public void Handle(StickersDidLoadedEventArgs e)
        {
            if (e.Type == StickerType.Image)
            {
                ProcessStickerSets(StickerType.Image);
            }
        }

        private void ProcessStickerSets(StickerType type)
        {
            var stickers = _stickersService.GetStickerSets(type);
            BeginOnUIThread(() =>
            {
                Items.ReplaceWith(stickers);
                SelectedItem = null;
                SelectedItem = _stickersService.GetGroupStickerSetById(_full?.StickerSet);
            });
        }

        public MvxObservableCollection<TLMessagesStickerSet> Items { get; private set; }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_shortName != _selectedItem?.Set.ShortName && !string.IsNullOrWhiteSpace(_shortName))
            {
                var stickerSet = _stickersService.GetStickerSetByName(_shortName);
                if (stickerSet == null)
                {
                    var stickerResponse = await ProtoService.GetStickerSetAsync(new TLInputStickerSetShortName { ShortName = _shortName });
                    if (stickerResponse.IsSucceeded)
                    {
                        stickerSet = stickerResponse.Result;
                    }
                }

                if (stickerSet != null)
                {
                    SelectedItem = Items.FirstOrDefault(x => x.Set.Id == stickerSet.Set.Id) ?? stickerSet;
                }
                else
                {
                    // TODO
                    return;
                }
            }

            var set = SelectedItem?.Set;
            var inputSet = set != null ? new TLInputStickerSetID { Id = set.Id, AccessHash = set.AccessHash } : (TLInputStickerSetBase)new TLInputStickerSetEmpty();

            var response = await ProtoService.SetStickersAsync(_item.ToInputChannel(), inputSet);
            if (response.IsSucceeded)
            {
                if (set != null)
                {
                    _stickersService.GetGroupStickerSetById(set);
                }

                Full.StickerSet = set;
                Full.HasStickerSet = set != null;
                Full.RaisePropertyChanged(() => Full.StickerSet);
                Full.RaisePropertyChanged(() => Full.HasStickerSet);

                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        public RelayCommand CancelCommand { get; }
        private void CancelExecute()
        {
            ShortName = null;
            SelectedItem = null;
        }
    }
}
