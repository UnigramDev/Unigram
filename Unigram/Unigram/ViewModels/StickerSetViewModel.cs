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
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : UnigramViewModelBase
    {
        private readonly DialogStickersViewModel _stickers;

        public StickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, DialogStickersViewModel stickers) 
            : base(protoService, cacheService, aggregator)
        {
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

            if (_stickerSet.IsInstalled && !_stickerSet.IsArchived && !_stickerSet.IsOfficial)
            {
                var response = await ProtoService.UninstallStickerSetAsync(new TLInputStickerSetID { Id = _stickerSet.Id, AccessHash = _stickerSet.AccessHash });
                if (response.IsSucceeded)
                {
                    _stickers.SyncStickers();

                    _stickerSet.IsInstalled = false;
                    _stickerSet.IsArchived = false;

                    RaisePropertyChanged(() => StickerSet);
                    IsLoading = false;
                }
            }
            else
            {
                var archive = _stickerSet.IsOfficial && !_stickerSet.IsArchived;

                var response = await ProtoService.InstallStickerSetAsync(new TLInputStickerSetID { Id = _stickerSet.Id, AccessHash = _stickerSet.AccessHash }, archive);
                if (response.IsSucceeded)
                {
                    _stickers.SyncStickers();

                    _stickerSet.IsInstalled = true;
                    _stickerSet.IsArchived = archive;

                    //if (response.Result is TLMessagesStickerSetInstallResultArchive archived)
                    //{
                    //    Debugger.Break();
                    //}
                    //else
                    //{
                    //    _stickerSet.IsInstalled = true;
                    //    _stickerSet.IsArchived = archive;
                    //}

                    RaisePropertyChanged(() => StickerSet);
                    IsLoading = false;
                }
            }
        }
    }
}
