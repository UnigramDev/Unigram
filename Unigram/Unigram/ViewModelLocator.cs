using Autofac;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Native;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.DeviceInfo;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Payments;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.SignIn;
using Unigram.ViewModels.Users;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using Unigram.ViewModels.Dialogs;

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

            container.Build((builder, account) =>
            {
                builder.RegisterType<MTProtoService>().WithParameter("account", account).As<IMTProtoService>().SingleInstance();
                builder.RegisterType<InMemoryCacheService>().As<ICacheService>().SingleInstance();
                builder.RegisterType<DeviceInfoService>().As<IDeviceInfoService>().SingleInstance();
                builder.RegisterType<UpdatesService>().As<IUpdatesService>().SingleInstance();
                builder.RegisterType<ConnectionService>().As<IConnectionService>().SingleInstance();
                builder.RegisterType<PublicConfigService>().As<IPublicConfigService>().SingleInstance();
                builder.RegisterType<TelegramEventAggregator>().As<ITelegramEventAggregator>().SingleInstance();

                // Files
                builder.RegisterType<DownloadFileManager>().As<IDownloadFileManager>().SingleInstance();
                builder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Audios)).As<IDownloadAudioFileManager>().SingleInstance();
                builder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Videos)).As<IDownloadVideoFileManager>().SingleInstance();
                builder.Register((ctx) => new DownloadDocumentFileManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Files)).As<IDownloadDocumentFileManager>().SingleInstance();
                builder.RegisterType<DownloadWebFileManager>().As<IDownloadWebFileManager>().SingleInstance();
                builder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Photos)).As<IUploadFileManager>().SingleInstance();
                builder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Audios)).As<IUploadAudioManager>().SingleInstance();
                builder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Videos)).As<IUploadVideoManager>().SingleInstance();
                builder.Register((ctx) => new UploadManager(ctx.Resolve<ITelegramEventAggregator>(), ctx.Resolve<IMTProtoService>(), ctx.Resolve<IStatsService>(), DataType.Files)).As<IUploadDocumentManager>().SingleInstance();

                builder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
                builder.RegisterType<LiveLocationService>().As<ILiveLocationService>().SingleInstance();
                builder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
                builder.RegisterType<PushService>().As<IPushService>().SingleInstance();
                builder.RegisterType<JumpListService>().As<IJumpListService>().SingleInstance();
                builder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
                //container.ContainerBuilder.RegisterType<GifsService>().As<IGifsService>().SingleInstance();
                builder.RegisterType<StickersService>().As<IStickersService>().SingleInstance();
                builder.RegisterType<StatsService>().As<IStatsService>().SingleInstance();
                builder.RegisterType<PlaybackService>().As<IPlaybackService>().SingleInstance();
                builder.RegisterType<PasscodeService>().As<IPasscodeService>().SingleInstance();
                builder.RegisterType<AppUpdateService>().As<IAppUpdateService>().SingleInstance();

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
                    builder.RegisterType<WindowsPhoneVibrationService>().As<IVibrationService>().SingleInstance();
                }
                else
                {
                    builder.RegisterType<FakeVibrationService>().As<IVibrationService>().SingleInstance();
                }

                // ViewModels
                builder.RegisterType<SignInViewModel>();
                builder.RegisterType<SignUpViewModel>();
                builder.RegisterType<SignInSentCodeViewModel>();
                builder.RegisterType<SignInPasswordViewModel>();
                builder.RegisterType<MainViewModel>().SingleInstance();
                builder.RegisterType<PlaybackViewModel>().SingleInstance();
                builder.RegisterType<ShareViewModel>().SingleInstance();
                builder.RegisterType<ForwardViewModel>().SingleInstance();
                builder.RegisterType<DialogShareLocationViewModel>().SingleInstance();
                builder.RegisterType<DialogsViewModel>().SingleInstance();
                builder.RegisterType<DialogViewModel>(); //.WithParameter((a, b) => a.Name == "dispatcher", (a, b) => WindowWrapper.Current().Dispatcher);
                builder.RegisterType<UserDetailsViewModel>();
                builder.RegisterType<UserCommonChatsViewModel>();
                builder.RegisterType<UserCreateViewModel>();
                builder.RegisterType<ChannelManageViewModel>();
                builder.RegisterType<ChannelAdminLogViewModel>();
                builder.RegisterType<ChannelAdminLogFilterViewModel>();
                builder.RegisterType<ChannelAdminRightsViewModel>();
                builder.RegisterType<ChannelBannedRightsViewModel>();
                builder.RegisterType<ChatDetailsViewModel>();// .SingleInstance();
                builder.RegisterType<ChatEditViewModel>();// .SingleInstance();
                builder.RegisterType<ChatInviteViewModel>();// .SingleInstance();
                builder.RegisterType<ChatInviteLinkViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelDetailsViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelEditViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelEditStickerSetViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelAdminsViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelBannedViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelKickedViewModel>();// .SingleInstance();
                builder.RegisterType<ChannelParticipantsViewModel>();// .SingleInstance();
                builder.RegisterType<DialogSharedMediaViewModel>(); // .SingleInstance();
                builder.RegisterType<UsersSelectionViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep2ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChannelCreateStep3ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChatCreateStep1ViewModel>(); //.SingleInstance();
                builder.RegisterType<ChatCreateStep2ViewModel>(); //.SingleInstance();
                builder.RegisterType<InstantViewModel>().SingleInstance();
                builder.RegisterType<SettingsViewModel>().SingleInstance();
                builder.RegisterType<SettingsGeneralViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneIntroViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneViewModel>().SingleInstance();
                builder.RegisterType<SettingsPhoneSentCodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsStatsViewModel>().SingleInstance();
                builder.RegisterType<FeaturedStickersViewModel>().SingleInstance();
                builder.RegisterType<SettingsUsernameViewModel>().SingleInstance();
                builder.RegisterType<SettingsEditNameViewModel>().SingleInstance();
                builder.RegisterType<SettingsSessionsViewModel>().SingleInstance();
                builder.RegisterType<SettingsBlockedUsersViewModel>().SingleInstance();
                builder.RegisterType<SettingsBlockUserViewModel>();
                builder.RegisterType<SettingsNotificationsViewModel>().SingleInstance();
                builder.RegisterType<SettingsDataAndStorageViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyAndSecurityViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyStatusTimestampViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyPhoneCallViewModel>().SingleInstance();
                builder.RegisterType<SettingsPrivacyChatInviteViewModel>().SingleInstance();
                builder.RegisterType<SettingsSecurityChangePasswordViewModel>(); //.SingleInstance();
                builder.RegisterType<SettingsSecurityPasscodeViewModel>().SingleInstance();
                builder.RegisterType<SettingsAccountsViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersFeaturedViewModel>().SingleInstance();
                builder.RegisterType<SettingsStickersArchivedViewModel>().SingleInstance();
                builder.RegisterType<SettingsMasksViewModel>().SingleInstance();
                builder.RegisterType<SettingsMasksArchivedViewModel>().SingleInstance();
                builder.RegisterType<SettingsWallPaperViewModel>().SingleInstance();
                builder.RegisterType<SettingsAppearanceViewModel>().SingleInstance();
                builder.RegisterType<SettingsLanguageViewModel>().SingleInstance();
                builder.RegisterType<AttachedStickersViewModel>();
                builder.RegisterType<StickerSetViewModel>();
                builder.RegisterType<AboutViewModel>().SingleInstance();
                builder.RegisterType<PaymentFormStep1ViewModel>();
                builder.RegisterType<PaymentFormStep2ViewModel>();
                builder.RegisterType<PaymentFormStep3ViewModel>();
                builder.RegisterType<PaymentFormStep4ViewModel>();
                builder.RegisterType<PaymentFormStep5ViewModel>();
                builder.RegisterType<PaymentReceiptViewModel>();

                return builder.Build();
            });

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
            //var transportService = UnigramContainer.Current.ResolveType<ITransportService>();
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
            updatesService.GetMessagesAsync = protoService.GetMessagesAsync;

            if (SettingsHelper.IsAuthorized)
            {
                updatesService.LoadStateAndUpdate(() => { });
            }

            protoService.AuthorizationRequired -= OnAuthorizationRequired;
            protoService.AuthorizationRequired += OnAuthorizationRequired;
            protoService.PropertyChanged -= OnPropertyChanged;
            protoService.PropertyChanged += OnPropertyChanged;

            //transportService.TransportConnecting -= OnTransportConnecting;
            //transportService.TransportConnecting += OnTransportConnecting;
            //transportService.TransportConnected -= OnTransportConnected;
            //transportService.TransportConnected += OnTransportConnected;

            ConnectionManager.Instance.CurrentNetworkTypeChanged -= OnConnectionNetworkTypeChanged;
            ConnectionManager.Instance.CurrentNetworkTypeChanged += OnConnectionNetworkTypeChanged;
            ConnectionManager.Instance.ConnectionStateChanged -= OnConnectionStateChanged;
            ConnectionManager.Instance.ConnectionStateChanged += OnConnectionStateChanged;
        }

        private void OnConnectionNetworkTypeChanged(ConnectionManager sender, object args)
        {
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
            if (protoService != null)
            {
                if (sender.ConnectionState == ConnectionState.Connected)
                {
                    protoService.SetMessageOnTime(0, null);
                }
                else if (sender.ConnectionState == ConnectionState.WaitingForNetwork || sender.CurrentNetworkType == ConnectionNeworkType.None)
                {
                    protoService.SetMessageOnTime(25, Strings.Android.WaitingForNetwork);
                }
                else
                {
                    protoService.SetMessageOnTime(25, SettingsHelper.IsProxyEnabled ? Strings.Android.ConnectingToProxy : Strings.Android.Connecting);
                }
            }
        }

        private void OnConnectionStateChanged(ConnectionManager sender, object args)
        {
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
            if (protoService != null)
            {
                if (sender.ConnectionState == ConnectionState.Connected)
                {
                    protoService.SetMessageOnTime(0, null);
                }
                else if (sender.ConnectionState == ConnectionState.WaitingForNetwork)
                {
                    protoService.SetMessageOnTime(25, Strings.Android.WaitingForNetwork);
                }
                else
                {
                    protoService.SetMessageOnTime(25, SettingsHelper.IsProxyEnabled ? Strings.Android.ConnectingToProxy : Strings.Android.Connecting);
                }
            }
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
            Telegram.Logs.Log.Write(string.Format("Unauthorized method={0} error={1} authKeyId={2}", e.MethodName, e.Error ?? (object)"null", e.AuthKeyId));

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
