using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.DeviceInfo;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;
using Telegram.Api.Services.FileManager;
using Telegram.Api;
using System.IO;
using Windows.Storage;
using Unigram.Views;
using Unigram.Core.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.SignIn;
using Unigram.Views.SignIn;
using Unigram.ViewModels.Settings;
using Unigram.Services;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
using Unigram.ViewModels.Payments;
using Windows.Foundation.Metadata;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.ViewManagement;

namespace Unigram
{
    public class ViewModelLocator
    {
        private UnigramContainer container;

        public ViewModelLocator()
        {
            container = UnigramContainer.Current;
        }

        public IHardwareService HardwareService => container.ResolveType<IHardwareService>();

        public void Configure()
        {
            InitializeLayer();

            container.Reset();

            // .SingleIstance() is required to register a singleton service.
            container.ContainerBuilder.RegisterType<MTProtoService>().As<IMTProtoService>().SingleInstance();
            container.ContainerBuilder.RegisterType<TelegramEventAggregator>().As<ITelegramEventAggregator>().SingleInstance();
            container.ContainerBuilder.RegisterType<InMemoryCacheService>().As<ICacheService>().SingleInstance();
            container.ContainerBuilder.RegisterType<DeviceInfoService>().As<IDeviceInfoService>().SingleInstance();
            container.ContainerBuilder.RegisterType<UpdatesService>().As<IUpdatesService>().SingleInstance();
            container.ContainerBuilder.RegisterType<TransportService>().As<ITransportService>().SingleInstance();
            container.ContainerBuilder.RegisterType<ConnectionService>().As<IConnectionService>().SingleInstance();

            // Files
            container.ContainerBuilder.RegisterType<DownloadFileManager>().As<IDownloadFileManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Audios)).As<IDownloadAudioFileManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Videos)).As<IDownloadVideoFileManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Files)).As<IDownloadDocumentFileManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<DownloadWebFileManager>().As<IDownloadWebFileManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Photos)).As<IUploadFileManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Audios)).As<IUploadAudioManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Videos)).As<IUploadVideoManager>().SingleInstance();
            container.ContainerBuilder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Files)).As<IUploadDocumentManager>().SingleInstance();

            container.ContainerBuilder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
            container.ContainerBuilder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
            container.ContainerBuilder.RegisterType<PushService>().As<IPushService>().SingleInstance();
            container.ContainerBuilder.RegisterType<JumpListService>().As<IJumpListService>().SingleInstance();
            container.ContainerBuilder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
            //container.ContainerBuilder.RegisterType<GifsService>().As<IGifsService>().SingleInstance();
            container.ContainerBuilder.RegisterType<StickersService>().As<IStickersService>().SingleInstance();
            container.ContainerBuilder.RegisterType<StatsService>().As<IStatsService>().SingleInstance();
            container.ContainerBuilder.RegisterType<AppUpdateService>().As<IAppUpdateService>().SingleInstance();

            // Disabled due to crashes on Mobile: 
            // The RPC server is unavailable.
            //if (ApiInformation.IsTypePresent("Windows.Devices.Haptics.VibrationDevice") || ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 4))
            //{
            //    // Introduced in Creators Update
            //    container.ContainerBuilder.RegisterType<VibrationService>().As<IVibrationService>().SingleInstance();
            //}
            //else
            if (ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice"))
            {
                // To keep vibration compatibility with Anniversary Update
                container.ContainerBuilder.RegisterType<WindowsPhoneVibrationService>().As<IVibrationService>().SingleInstance();
            }
            else
            {
                container.ContainerBuilder.RegisterType<FakeVibrationService>().As<IVibrationService>().SingleInstance();
            }

            // ViewModels
            container.ContainerBuilder.RegisterType<IntroViewModel>();
            container.ContainerBuilder.RegisterType<SignInViewModel>();
            container.ContainerBuilder.RegisterType<SignUpViewModel>();
            container.ContainerBuilder.RegisterType<SignInSentCodeViewModel>();
            container.ContainerBuilder.RegisterType<SignInPasswordViewModel>();
            container.ContainerBuilder.RegisterType<MainViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<ShareViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<ForwardViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSendLocationViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogViewModel>();
            container.ContainerBuilder.RegisterType<DialogStickersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<UserDetailsViewModel>();
            container.ContainerBuilder.RegisterType<ChannelManageViewModel>();
            container.ContainerBuilder.RegisterType<ChannelAdminLogViewModel>();
            container.ContainerBuilder.RegisterType<ChannelAdminLogFilterViewModel>();
            container.ContainerBuilder.RegisterType<ChannelAdminRightsViewModel>();
            container.ContainerBuilder.RegisterType<ChannelBannedRightsViewModel>();
            container.ContainerBuilder.RegisterType<UserCommonChatsViewModel>();
            container.ContainerBuilder.RegisterType<ChatDetailsViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChatInviteViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChatInviteLinkViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelDetailsViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelEditViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelEditTypeViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelEditStickerSetViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelAdminsViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelBannedViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelKickedViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<ChannelParticipantsViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSharedMediaViewModel>(); // .SingleInstance();
            container.ContainerBuilder.RegisterType<UsersSelectionViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChannelStep1ViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChannelStep2ViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChatStep1ViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChatStep2ViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<InstantViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsGeneralViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPhoneViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPhoneSentCodeViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStorageViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStatsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<FeaturedStickersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsUsernameViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsEditNameViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsSessionsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsBlockedUsersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsBlockUserViewModel>();
            container.ContainerBuilder.RegisterType<SettingsNotificationsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsDataAndStorageViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPrivacyAndSecurityViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPrivacyStatusTimestampViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPrivacyPhoneCallViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsPrivacyChatInviteViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsSecurityChangePasswordViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsAccountsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStickersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStickersFeaturedViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStickersArchivedViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsMasksViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsMasksArchivedViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsWallPaperViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<AttachedStickersViewModel>();
            container.ContainerBuilder.RegisterType<StickerSetViewModel>();
            container.ContainerBuilder.RegisterType<AboutViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<PaymentFormStep1ViewModel>();
            container.ContainerBuilder.RegisterType<PaymentFormStep2ViewModel>();
            container.ContainerBuilder.RegisterType<PaymentFormStep3ViewModel>();
            container.ContainerBuilder.RegisterType<PaymentFormStep4ViewModel>();
            container.ContainerBuilder.RegisterType<PaymentFormStep5ViewModel>();
            container.ContainerBuilder.RegisterType<PaymentReceiptViewModel>();

            container.Build();

            Task.Run(() => LoadStateAndUpdate());
        }

        private void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(FileUtils.GetFileName(path)))
                {
                    File.Delete(FileUtils.GetFileName(path));
                }
            }
            catch { }
        }

        private void InitializeLayer()
        {
            if (SettingsHelper.SupportedLayer < 71 || !SettingsHelper.IsAuthorized)
            {
                DeleteIfExists("database.sqlite");
                ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", 0L);
                ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", 0L);
                ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeMask", 0L);
                ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeFavs", 0L);
            }

            SettingsHelper.SupportedLayer = Telegram.Api.Constants.SupportedLayer;

            //if (SettingsHelper.SupportedLayer != Constants.SupportedLayer ||
            //    SettingsHelper.DatabaseVersion != Constants.DatabaseVersion)
            {
                //SettingsHelper.SupportedLayer = Constants.SupportedLayer;
                //SettingsHelper.DatabaseVersion = Constants.DatabaseVersion;

                DeleteIfExists("action_queue.dat");
                DeleteIfExists("action_queue.dat.temp");
                DeleteIfExists("chats.dat");
                DeleteIfExists("chats.dat.temp");
                DeleteIfExists("dialogs.dat");
                DeleteIfExists("dialogs.dat.temp");
                DeleteIfExists("state.dat");
                DeleteIfExists("state.dat.temp");
                DeleteIfExists("users.dat");
                DeleteIfExists("users.dat.temp");

                DeleteIfExists("temp_chats.dat");
                DeleteIfExists("temp_dialogs.dat");
                DeleteIfExists("temp_difference.dat");
                DeleteIfExists("temp_state.dat");
                DeleteIfExists("temp_users.dat");
            }
        }

        public void LoadStateAndUpdate()
        {
            var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>() as MTProtoService;
            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            var transportService = UnigramContainer.Current.ResolveType<ITransportService>();
            //cacheService.Init();
            updatesService.GetCurrentUserId = () => protoService.CurrentUserId;
            updatesService.GetStateAsync = protoService.GetStateAsync;
            updatesService.GetDHConfigAsync = protoService.GetDHConfigAsync;
            updatesService.GetDifferenceAsync = protoService.GetDifferenceAsync;
            //updatesService.AcceptEncryptionAsync = protoService.AcceptEncryptionCallback;
            //updatesService.SendEncryptedServiceAsync = protoService.SendEncryptedServiceCallback;
            updatesService.SetMessageOnTimeAsync = protoService.SetMessageOnTime;
            updatesService.UpdateChannelAsync = protoService.UpdateChannelAsync;
            updatesService.GetParticipantAsync = protoService.GetParticipantAsync;
            updatesService.GetFullUserAsync = protoService.GetFullUserAsync;
            updatesService.GetFullChatAsync = protoService.GetFullChatAsync;
            updatesService.GetChannelMessagesAsync = protoService.GetMessagesAsync;
            updatesService.LoadStateAndUpdate(() => { });

            protoService.AuthorizationRequired -= OnAuthorizationRequired;
            protoService.AuthorizationRequired += OnAuthorizationRequired;
            protoService.PropertyChanged -= OnPropertyChanged;
            protoService.PropertyChanged += OnPropertyChanged;

            transportService.TransportConnecting -= OnTransportConnecting;
            transportService.TransportConnecting += OnTransportConnecting;
            transportService.TransportConnected -= OnTransportConnected;
            transportService.TransportConnected += OnTransportConnected;
        }

        private async void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Message"))
            {
                var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
                if (protoService != null)
                {
                    if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                    {
                        var statusBar = StatusBar.GetForCurrentView();
                        if (string.IsNullOrEmpty(protoService.Message))
                        {
                            statusBar.ProgressIndicator.Text = string.Empty;
                            await statusBar.ProgressIndicator.HideAsync();
                        }
                        else
                        {
                            statusBar.ProgressIndicator.Text = protoService.Message;
                            await statusBar.ProgressIndicator.ShowAsync();
                        }
                    }
                    else
                    {
                        ApplicationView.GetForCurrentView().Title = protoService.Message ?? string.Empty;
                    }
                }
            }
        }

        private void OnTransportConnecting(object sender, TransportEventArgs e)
        {
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
            if (protoService != null)
            {
                protoService.SetMessageOnTime(25, SettingsHelper.IsProxyEnabled ? "Connecting to proxy..." : "Connecting...");
            }
        }

        private void OnTransportConnected(object sender, TransportEventArgs e)
        {
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
            if (protoService != null)
            {
                protoService.SetMessageOnTime(0, null);
            }
        }

        private void OnAuthorizationRequired(object sender, AuthorizationRequiredEventArgs e)
        {
            DeleteIfExists("database.sqlite");

            SettingsHelper.IsAuthorized = false;
            SettingsHelper.UserId = 0;
            SettingsHelper.ChannelUri = null;
            MTProtoService.Current.CurrentUserId = 0;

            ApplicationSettings.Current.AddOrUpdateValue("lastGifLoadTime", 0L);
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTime", 0L);
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeMask", 0L);
            ApplicationSettings.Current.AddOrUpdateValue("lastStickersLoadTimeFavs", 0L);

            Debug.WriteLine("!!! UNAUTHORIZED !!!");

            Execute.BeginOnUIThread(() =>
            {
                var type = App.Current.NavigationService.CurrentPageType;
                if (type.Name.StartsWith("SignIn") || type.Name.StartsWith("SignUp")) { }
                else
                {
                    try
                    {
                        UnigramContainer.Current.ResolveType<MainViewModel>().Refresh = true;
                    }
                    catch { }

                    App.Current.NavigationService.Navigate(typeof(IntroPage));
                    App.Current.NavigationService.Frame.BackStack.Clear();
                }
            });
        }
    }
}
