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
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Helpers;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Strings;
using Unigram.Views;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
   public class SettingsViewModel : UnigramViewModelBase
    {
        private readonly IUpdatesService _updatesService;
        private readonly IPushService _pushService;
        private readonly IContactsService _contactsService;
        private readonly IUploadFileManager _uploadFileManager;
        private readonly IStickersService _stickersService;

        public SettingsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUpdatesService updatesService, IPushService pushService, IContactsService contactsService, IUploadFileManager uploadFileManager, IStickersService stickersService) 
            : base(protoService, cacheService, aggregator)
        {
            _updatesService = updatesService;
            _pushService = pushService;
            _contactsService = contactsService;
            _uploadFileManager = uploadFileManager;
            _stickersService = stickersService;

            AskCommand = new RelayCommand(AskExecute);
            LogoutCommand = new RelayCommand(LogoutExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
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
                    var result = response.Result.FirstOrDefault() as TLUser;
                    if (result != null)
                    {
                        Self = result;
                        SettingsHelper.UserId = result.Id;
                    }
                }
            }

            var user = Self;
            if (user == null)
            {
                return;
            }

            var full = CacheService.GetFullUser(user.Id);
            if (full == null)
            {
                var response = await ProtoService.GetFullUserAsync(user.ToInputUser());
                if (response.IsSucceeded)
                {
                    full = response.Result;
                }
            }

            Full = full;
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

        private TLUserFull _full;
        public TLUserFull Full
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

            //var fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 640, 0.77);

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name);
            if (upload != null)
            {
                var response = await ProtoService.UploadProfilePhotoAsync(upload.ToInputFile() as TLInputFile);
                if (response.IsSucceeded)
                {
                    var user = Self as TLUser;
                    if (user == null)
                    {
                        return;
                    }

                    var userFull = CacheService.GetFullUser(user.Id);
                    if (userFull == null)
                    {
                        return;
                    }

                    userFull.HasProfilePhoto = true;
                    userFull.ProfilePhoto = response.Result.Photo;
                    userFull.RaisePropertyChanged(() => userFull.ProfilePhoto);
                }
            }
        }

        public RelayCommand AskCommand { get; }
        private async void AskExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AskAQuestionInfo, Strings.Android.AskAQuestion, Strings.Android.AskButton, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.GetSupportAsync();
                if (response.IsSucceeded)
                {
                    NavigationService.NavigateToDialog(response.Result.User);
                }
            }
        }

        public RelayCommand LogoutCommand { get; }
        private async void LogoutExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureLogout, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            await _pushService.UnregisterAsync();

            var response = await ProtoService.LogOutAsync();
            if (response.IsSucceeded)
            {
                await _contactsService.RemoveAsync();

                SettingsHelper.IsAuthorized = false;
                SettingsHelper.UserId = 0;
                //ProtoService.ClearQueue();
                _updatesService.ClearState();
                _stickersService.Cleanup();
                CacheService.ClearAsync();
                CacheService.ClearConfigImportAsync();

                App.Current.Exit();
            }
            else
            {

            }
        }
    }
}
