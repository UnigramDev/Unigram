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
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Helpers;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
   public class SettingsViewModel : UnigramViewModelBase
    {
        private readonly IUploadFileManager _uploadFileManager;

        public SettingsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager) 
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var cached = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            if (cached != null)
            {
                Self = cached;
            }
            else
            {
                var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                if (response.IsSucceeded)
                {
                    var user = response.Result.FirstOrDefault() as TLUser;
                    if (user != null)
                    {
                        Self = user;
                    }
                }
            }
        }

        private TLUser _self;
        public TLUser Self
        {
            get
            {
                return _self;
            }
            set
            {
                Set(ref _self, value);
            }
        }

        public bool IsSendByEnterEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsSendByEnterEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsSendByEnterEnabled = value;
            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand => new RelayCommand<StorageFile>(EditPhotoExecute);
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

            //var fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 640, 0.77);

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false);
            if (upload != null)
            {
                var response = await ProtoService.UploadProfilePhotoAsync(upload.ToInputFile() as TLInputFile);
                if (response.IsSucceeded)
                {
                    var photo = response.Result.Photo as TLPhoto;
                }
            }
        }

#if DEBUG

        public RelayCommand DeleteAccountCommand => new RelayCommand(DeleteAccountExecute);
        private async void DeleteAccountExecute()
        {
            // THIS CODE WILL RUN ONLY IF FIRST CONFIGURED SERVER IP IS TEST SERVER
            if (Telegram.Api.Constants.FirstServerIpAddress.Equals("149.154.167.40"))
            {
                var dialog = new InputDialog();
                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary && dialog.Text.Equals(Self.Phone) && Self.Username != "frayxrulez")
                {
                    var really = await TLMessageDialog.ShowAsync("REAAAALLY???", "REALLYYYY???", "YES", "NO I DON'T WANT TO");
                    if (really == ContentDialogResult.Primary)
                    {
                        await ProtoService.DeleteAccountAsync("Testing registration");
                        App.Current.Exit();
                    }
                }
            }
        }

#endif
    }
}
