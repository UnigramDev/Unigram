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

        public RelayCommand AskCommand => new RelayCommand(AskExecute);
        private async void AskExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync("Plase note that Telegram Support is done by volunteers. We try to respond as quickly as possible, but it may take a while.\n\nPlase take a look at the Telegram FAQ: it has important troubleshooting tips and answers to most questions.", "Telegram", "FAQ", "OK");
            if (confirm == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://telegram.org/faq"));
            }
            else
            {
                var response = await ProtoService.GetSupportAsync();
                if (response.IsSucceeded)
                {
                    NavigationService.NavigateToDialog(response.Result.User);
                }
            }
        }

        public RelayCommand LogoutCommand => new RelayCommand(LogoutExecute);
        private async void LogoutExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync("Are you sure you want to logout?", "Unigram", "OK", "Cancel");
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            await _pushService.UnregisterAsync();

            var response = await ProtoService.LogOutAsync();
            if (response.IsSucceeded)
            {
                await _contactsService.UnsyncContactsAsync();

                SettingsHelper.IsAuthorized = false;
                SettingsHelper.UserId = 0;
                ProtoService.ClearQueue();
                _updatesService.ClearState();
                _stickersService.Cleanup();
                CacheService.ClearAsync();
                CacheService.ClearConfigImportAsync();

                await TLMessageDialog.ShowAsync("The app will be closed. Relaunch it to login again.", "Unigram", "OK");
                App.Current.Exit();
            }
            else
            {

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
