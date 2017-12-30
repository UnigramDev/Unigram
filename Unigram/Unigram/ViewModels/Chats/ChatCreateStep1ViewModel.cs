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
using Unigram.Common;
using Unigram.Views.Chats;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using ChatCreateStep2Tuple = Telegram.Api.TL.TLTuple<string, Telegram.Api.TL.TLInputFileBase>;

namespace Unigram.ViewModels.Chats
{
    public class ChatCreateStep1ViewModel : UnigramViewModelBase
    {
        private IUploadFileManager _uploadFileManager;

        private bool _uploadingPhoto;
        private Action _uploadingCallback;
        private TLInputFileBase _photo;

        public ChatCreateStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
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
        private void SendExecute()
        {
            if (_photo != null)
            {
                ContinueUploadingPhoto();
            }
            else if (_uploadingPhoto)
            {
                _uploadingCallback = () => ContinueUploadingPhoto();
            }
            else
            {
                NavigationService.Navigate(typeof(ChatCreateStep2Page), new ChatCreateStep2Tuple(_title, null));
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

        private void ContinueUploadingPhoto()
        {
            NavigationService.Navigate(typeof(ChatCreateStep2Page), new ChatCreateStep2Tuple(_title, _photo));
        }
    }
}
