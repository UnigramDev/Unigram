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
using Unigram.Core.Dependency;
using Unigram.Core.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Login;
using Unigram.Views.Login;
using Unigram.ViewModels.Settings;
using Unigram.Services;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;

namespace Unigram
{
    public class ViewModelLocator
    {
        private UnigramContainer container;

        public ViewModelLocator()
        {
            container = UnigramContainer.Instance;
        }

        public void Configure()
        {
            InitializeLayer();

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
            container.ContainerBuilder.RegisterType<DownloadDocumentFileManager>().As<IDownloadDocumentFileManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<UploadManager>().As<IUploadFileManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<UploadManager>().As<IUploadAudioManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<UploadManager>().As<IUploadDocumentManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<UploadManager>().As<IUploadVideoManager>().SingleInstance();

            container.ContainerBuilder.RegisterType<ContactsService>().As<IContactsService>().SingleInstance();
            container.ContainerBuilder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
            container.ContainerBuilder.RegisterType<PushService>().As<IPushService>().SingleInstance();
            container.ContainerBuilder.RegisterType<JumpListService>().As<IJumpListService>().SingleInstance();
            container.ContainerBuilder.RegisterType<HardwareService>().As<IHardwareService>().SingleInstance();
            container.ContainerBuilder.RegisterType<GifsService>().As<IGifsService>().SingleInstance();

            // ViewModels
            container.ContainerBuilder.RegisterType<LoginWelcomeViewModel>();
            container.ContainerBuilder.RegisterType<LoginPhoneNumberViewModel>();
            container.ContainerBuilder.RegisterType<LoginPhoneCodeViewModel>();
            container.ContainerBuilder.RegisterType<LoginPasswordViewModel>();
            container.ContainerBuilder.RegisterType<MainViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSendLocationViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogViewModel>();
            container.ContainerBuilder.RegisterType<UserInfoViewModel>();
            container.ContainerBuilder.RegisterType<ChatInfoViewModel>();// .SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSharedMediaViewModel>(); // .SingleInstance();
            container.ContainerBuilder.RegisterType<UsersSelectionViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChannelStep1ViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChannelStep2ViewModel>(); //.SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChatStep1ViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<CreateChatStep2ViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<ArticleViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsStorageViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<FeaturedStickersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsUsernameViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsEditNameViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsSessionsViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsBlockedUsersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsBlockUserViewModel>();
            container.ContainerBuilder.RegisterType<SettingsFeaturedStickersViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<SettingsNotificationsViewModel>().SingleInstance();

            container.Build();

            Task.Run(() => LoadStateAndUpdate());
        }

        private void InitializeLayer()
        {
            var deleteIfExists = new Action<string>((path) =>
            {
                if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, path)))
                {
                    File.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, path));
                }
            });

            //if (SettingsHelper.SupportedLayer != Constants.SupportedLayer ||
            //    SettingsHelper.DatabaseVersion != Constants.DatabaseVersion)
            {
                //SettingsHelper.SupportedLayer = Constants.SupportedLayer;
                //SettingsHelper.DatabaseVersion = Constants.DatabaseVersion;

                deleteIfExists("action_queue.dat");
                deleteIfExists("action_queue.dat.temp");
                deleteIfExists("chats.dat");
                deleteIfExists("chats.dat.temp");
                deleteIfExists("dialogs.dat");
                deleteIfExists("dialogs.dat.temp");
                deleteIfExists("state.dat");
                deleteIfExists("state.dat.temp");
                deleteIfExists("users.dat");
                deleteIfExists("users.dat.temp");

                deleteIfExists("temp_chats.dat");
                deleteIfExists("temp_dialogs.dat");
                deleteIfExists("temp_difference.dat");
                deleteIfExists("temp_state.dat");
                deleteIfExists("temp_users.dat");
            }
        }

        public void LoadStateAndUpdate()
        {
            var cacheService = UnigramContainer.Instance.ResolveType<ICacheService>();
            var protoService = UnigramContainer.Instance.ResolveType<IMTProtoService>();
            var updatesService = UnigramContainer.Instance.ResolveType<IUpdatesService>();
            cacheService.Init();
            updatesService.GetCurrentUserId = () => protoService.CurrentUserId;
            updatesService.GetStateAsync = protoService.GetStateCallback;
            updatesService.GetDHConfigAsync = protoService.GetDHConfigCallback;
            updatesService.GetDifferenceAsync = protoService.GetDifferenceCallback;
            //updatesService.AcceptEncryptionAsync = protoService.AcceptEncryptionCallback;
            //updatesService.SendEncryptedServiceAsync = protoService.SendEncryptedServiceCallback;
            updatesService.SetMessageOnTimeAsync = protoService.SetMessageOnTime;
            updatesService.UpdateChannelAsync = protoService.UpdateChannelCallback;
            updatesService.GetParticipantAsync = protoService.GetParticipantCallback;
            updatesService.GetFullUserAsync = protoService.GetFullUserCallback;
            updatesService.GetFullChatAsync = protoService.GetFullChatCallback;
            updatesService.GetChannelMessagesAsync = protoService.GetMessagesCallback;
            updatesService.LoadStateAndUpdate(() => { });

            protoService.AuthorizationRequired += (s, e) =>
            {
                SettingsHelper.IsAuthorized = false;
                Debugger.Break();

                Execute.BeginOnUIThread(() =>
                {
                    App.Current.NavigationService.Navigate(typeof(LoginWelcomePage));
                    App.Current.NavigationService.Frame.BackStack.Clear();
                });
            };
        }
    }
}
