using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelEditViewModel : ChannelDetailsViewModel
    {
        private readonly IUploadFileManager _uploadFileManager;

        public ChannelEditViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;

            SendCommand = new RelayCommand(SendExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            EditTypeCommand = new RelayCommand(EditTypeExecute);
            EditStickerSetCommand = new RelayCommand(EditStickerSetExecute);
        }

        public bool CanEditSignatures
        {
            get
            {
                return _item != null && _item.IsBroadcast;
            }
        }

        public bool CanEditHiddenPreHistory
        {
            get
            {
                return _item != null && _full != null && _item.IsMegaGroup && !_item.HasUsername;
            }
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
            }
        }

        private string _about;
        public string About
        {
            get
            {
                return _about;
            }
            set
            {
                Set(ref _about, value);
            }
        }

        private bool _isSignatures;
        public bool IsSignatures
        {
            get
            {
                return _isSignatures;
            }
            set
            {
                Set(ref _isSignatures, value);
            }
        }
        private bool _isHiddenPreHistory;
        public bool IsHiddenPreHistory
        {
            get
            {
                return _isHiddenPreHistory;
            }
            set
            {
                Set(ref _isHiddenPreHistory, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;
            Title = null;
            About = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;
                Title = _item.Title;
                IsSignatures = _item.IsSignatures;

                RaisePropertyChanged(() => CanEditSignatures);

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
                    About = _full.About;
                    IsHiddenPreHistory = _full.IsHiddenPreHistory;

                    RaisePropertyChanged(() => CanEditHiddenPreHistory);
                }
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var about = _about.Format();
            var title = _title.Trim();

            if (_item != null && _full != null && !string.Equals(about, _full.About))
            {
                var response = await ProtoService.EditAboutAsync(_item, about);
                if (response.IsSucceeded)
                {
                    _full.About = about;
                    _full.RaisePropertyChanged(() => _full.About);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && !string.Equals(title, _item.Title))
            {
                var response = await ProtoService.EditTitleAsync(_item, title);
                if (response.IsSucceeded)
                {
                    _item.Title = title;
                    _item.RaisePropertyChanged(() => _item.Title);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && _isSignatures != _item.IsSignatures)
            {
                var response = await ProtoService.ToggleSignaturesAsync(_item.ToInputChannel(), _isSignatures);
                if (response.IsSucceeded)
                {

                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && _full != null && _isHiddenPreHistory != _full.IsHiddenPreHistory)
            {
                var response = await ProtoService.TogglePreHistoryHiddenAsync(_item.ToInputChannel(), _isHiddenPreHistory);
                if (response.IsSucceeded)
                {

                }
                else
                {
                    // TODO:
                    return;
                }
            }

            NavigationService.GoBack();
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false);
            if (upload != null)
            {
                var response = await ProtoService.EditPhotoAsync(_item, new TLInputChatUploadedPhoto { File = upload.ToInputFile() });
                if (response.IsSucceeded)
                {

                }
            }
        }

        public RelayCommand EditTypeCommand { get; }
        private void EditTypeExecute()
        {
            NavigationService.Navigate(typeof(ChannelEditTypePage), _item.ToPeer());
        }

        public RelayCommand EditStickerSetCommand { get; }
        private void EditStickerSetExecute()
        {
            NavigationService.Navigate(typeof(ChannelEditStickerSetPage), _item.ToPeer());
        }
    }
}
