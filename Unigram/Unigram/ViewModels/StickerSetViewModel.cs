using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : UnigramViewModelBase
    {
        private readonly IStickersService _stickersService;
        private readonly DialogStickersViewModel _stickers;

        public StickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService, DialogStickersViewModel stickers) 
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;
            _stickers = stickers;

            Items = new ObservableCollection<TLMessagesStickerSet>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputStickerSetBase set)
            {
                IsLoading = true;

                var response = await ProtoService.GetStickerSetAsync(set);
                if (response.IsSucceeded)
                {
                    StickerSet = response.Result.Set;
                    Items.Clear();
                    Items.Add(response.Result);

                    IsLoading = false;

                    if (_stickersService.IsStickerPackInstalled(response.Result.Set.Id))
                    {
                        var existing = _stickersService.GetStickerSetById(response.Result.Set.Id);
                        if (existing.Set.Hash != response.Result.Set.Hash)
                        {
                            _stickersService.LoadStickers(response.Result.Set.IsMasks ? StickerType.Mask : StickerType.Image, false, true);
                        }
                    }
                }
                else
                {
                    StickerSet = new TLStickerSet();
                    Items.Clear();
                    Items.Add(new TLMessagesStickerSet { Set = new TLStickerSet { Title = "Sticker pack not found." } });

                    //IsLoading = false;
                }
            }
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                Set(ref _isLoading, value);
            }
        }

        private TLStickerSet _stickerSet = new TLStickerSet();
        public TLStickerSet StickerSet
        {
            get
            {
                return _stickerSet;
            }
            set
            {
                Set(ref _stickerSet, value);
            }
        }

        public ObservableCollection<TLMessagesStickerSet> Items { get; private set; }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            IsLoading = true;

            if (_stickersService.IsStickerPackInstalled(_stickerSet.Id) == false)
            {
                var response = await ProtoService.InstallStickerSetAsync(new TLInputStickerSetID { Id = _stickerSet.Id, AccessHash = _stickerSet.AccessHash }, false);
                if (response.IsSucceeded)
                {
                    _stickersService.LoadStickers(_stickerSet.IsMasks ? StickerType.Mask : StickerType.Image, false, true);

                    _stickerSet.IsInstalled = true;
                    _stickerSet.IsArchived = false;

                    NavigationService.GoBack();
                }
            }
            else
            {
                if (_stickerSet.IsOfficial)
                {
                    _stickersService.RemoveStickersSet(_stickerSet, 1, true);
                    NavigationService.GoBack();
                }
                else
                {
                    _stickersService.RemoveStickersSet(_stickerSet, 0, true);
                    NavigationService.GoBack();
                }
            }

            //if (_stickerSet.IsInstalled && !_stickerSet.IsArchived && !_stickerSet.IsOfficial)
            //{
            //    var response = await ProtoService.UninstallStickerSetAsync(new TLInputStickerSetID { Id = _stickerSet.Id, AccessHash = _stickerSet.AccessHash });
            //    if (response.IsSucceeded)
            //    {
            //        _stickersService.LoadStickers(_stickerSet.IsMasks ? StickersService.TYPE_MASK : StickersService.TYPE_IMAGE, false, true);

            //        _stickerSet.IsInstalled = false;
            //        _stickerSet.IsArchived = false;

            //        RaisePropertyChanged(() => StickerSet);
            //        IsLoading = false;
            //    }
            //}
            //else
            //{
            //    var archive = _stickerSet.IsOfficial && !_stickerSet.IsArchived;

            //    var response = await ProtoService.InstallStickerSetAsync(new TLInputStickerSetID { Id = _stickerSet.Id, AccessHash = _stickerSet.AccessHash }, archive);
            //    if (response.IsSucceeded)
            //    {
            //        _stickersService.LoadStickers(_stickerSet.IsMasks ? StickersService.TYPE_MASK : StickersService.TYPE_IMAGE, false, true);

            //        _stickerSet.IsInstalled = true;
            //        _stickerSet.IsArchived = archive;

            //        //if (response.Result is TLMessagesStickerSetInstallResultArchive archived)
            //        //{
            //        //    Debugger.Break();
            //        //}
            //        //else
            //        //{
            //        //    _stickerSet.IsInstalled = true;
            //        //    _stickerSet.IsArchived = archive;
            //        //}

            //        RaisePropertyChanged(() => StickerSet);
            //        IsLoading = false;
            //    }
            //}
        }
    }
}
