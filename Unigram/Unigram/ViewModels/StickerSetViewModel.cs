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
using Telegram.Api.TL.Messages;
using Template10.Utils;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : UnigramViewModelBase
    {
        private readonly IStickersService _stickersService;

        public StickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;

            Items = new MvxObservableCollection<TLDocumentBase>();

            SendCommand = new RelayCommand(SendExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputStickerSetBase set)
            {
                IsLoading = true;

                var response = await ProtoService.GetStickerSetAsync(set);
                if (response.IsSucceeded)
                {
                    IsLoading = false;

                    if (_stickersService.IsStickerPackInstalled(response.Result.Set.Id))
                    {
                        var existing = _stickersService.GetStickerSetById(response.Result.Set.Id);
                        if (existing.Set.Hash != response.Result.Set.Hash)
                        {
                            StickerSet = response.Result.Set;
                            Items.ReplaceWith(response.Result.Documents);

                            _stickersService.LoadStickers(response.Result.Set.IsMasks ? StickerType.Mask : StickerType.Image, false, true);
                        }
                        else
                        {
                            StickerSet = existing.Set;
                            Items.ReplaceWith(existing.Documents);
                        }
                    }
                    else
                    {
                        StickerSet = response.Result.Set;
                        Items.ReplaceWith(response.Result.Documents);
                    }
                }
                else
                {
                    StickerSet = new TLStickerSet { Title = "Sticker pack not found." };
                    Items.Clear();

                    //IsLoading = false;
                }
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

        public MvxObservableCollection<TLDocumentBase> Items { get; private set; }

        public RelayCommand SendCommand { get; }
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
                    _stickerSet.RaisePropertyChanged(() => _stickerSet.IsInstalled);

                    NavigationService.GoBack();
                }
            }
            else
            {
                _stickersService.RemoveStickersSet(_stickerSet, _stickerSet.IsOfficial ? 1 : 0, true);

                _stickerSet.IsInstalled = _stickerSet.IsOfficial;
                _stickerSet.IsArchived = _stickerSet.IsOfficial;
                _stickerSet.RaisePropertyChanged(() => _stickerSet.IsInstalled);

                NavigationService.GoBack();
            }
        }
    }
}
