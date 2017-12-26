using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels.Methods;
using Unigram.Common;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelCreateStep1ViewModel : UnigramViewModelBase
    {
        private IUploadFileManager _uploadFileManager;

        private bool _uploadingPhoto;
        private Action _uploadingCallback;
        private TLInputFileBase _photo;

        public ChannelCreateStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager) 
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;

            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title));
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
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
                SendCommand.RaiseCanExecuteChanged();
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

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get
            {
                return _preview;
            }
            set
            {
                Set(ref _preview, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await ProtoService.CreateChannelAsync(TLChannelsCreateChannel.Flag.Broadcast, _title, _about);
            if (response.IsSucceeded)
            {
                if (response.Result is TLUpdates updates)
                {
                    if (updates.Chats.FirstOrDefault() is TLChannel channel)
                    {
                        if (_photo != null)
                        {
                            ContinueUploadingPhoto(channel);
                        }
                        else if (_uploadingPhoto)
                        {
                            _uploadingCallback = () => ContinueUploadingPhoto(channel);
                        }
                        else
                        {
                            NavigationService.Navigate(typeof(ChannelCreateStep2Page), channel.ToPeer());
                        }
                    }
                }
            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            _uploadingPhoto = true;

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

            Preview = new BitmapImage(FileUtils.GetTempFileUri(fileName));

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name);
            if (upload != null)
            {
                _photo = upload.ToInputFile();
                _uploadingPhoto = false;
                _uploadingCallback?.Invoke();
            }
        }

        private async void ContinueUploadingPhoto(TLChannel channel)
        {
            var response = await ProtoService.EditPhotoAsync(channel, new TLInputChatUploadedPhoto { File = _photo });
            if (response.IsSucceeded)
            {
                NavigationService.Navigate(typeof(ChannelCreateStep2Page), channel.ToPeer());
            }
            else
            {
                // TODO: ...
            }
        }
    }
}
