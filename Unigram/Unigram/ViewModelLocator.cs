namespace Unigram
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Autofac;
    using Core.Dependency;
    using Core.Services;
    using Telegram.Api.Aggregator;
    using Telegram.Api.Helpers;
    using Telegram.Api.Services;
    using Telegram.Api.Services.Cache;
    using Telegram.Api.Services.Connection;
    using Telegram.Api.Services.DeviceInfo;
    using Telegram.Api.Services.Updates;
    using Telegram.Api.TL;
    using Telegram.Api.Transport;
    using ViewModels;
    using Telegram.Api.Services.FileManager;
    using Telegram.Api;
    using System.IO;
    using Windows.Storage;

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

            container.ContainerBuilder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();
            container.ContainerBuilder.RegisterType<PushService>().As<IPushService>().SingleInstance();
            container.ContainerBuilder.RegisterType<JumpListService>().As<IJumpListService>().SingleInstance();

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
            container.ContainerBuilder.RegisterType<SettingsViewModel>().SingleInstance();

            container.Build();

            Initialize();
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

        private void Initialize()
        {
            Execute.Initialize();

            var cacheService = UnigramContainer.Instance.ResolverType<ICacheService>();
            var protoService = UnigramContainer.Instance.ResolverType<IMTProtoService>();
            var updatesService = UnigramContainer.Instance.ResolverType<IUpdatesService>();
            cacheService.Init();
            updatesService.GetCurrentUserId = () => protoService.CurrentUserId;
            updatesService.GetStateAsync = protoService.GetStateCallback;
            updatesService.GetDHConfigAsync = protoService.GetDHConfigCallback;
            updatesService.GetDifferenceAsync = protoService.GetDifferenceCallback;
            //updatesService.AcceptEncryptionAsync = protoService.AcceptEncryptionCallback;
            //updatesService.SendEncryptedServiceAsync = protoService.SendEncryptedServiceCallback;
            updatesService.SetMessageOnTimeAsync = protoService.SetMessageOnTime;
            //updatesService.RemoveFromQueue = protoService.RemoveFromQueue;
            updatesService.UpdateChannelAsync = protoService.UpdateChannelCallback;
            updatesService.GetParticipantAsync = protoService.GetParticipantCallback;
            updatesService.GetFullChatAsync = protoService.GetFullChatCallback;
            updatesService.LoadStateAndUpdate(() => { });

            protoService.AuthorizationRequired += (s, e) =>
            {
                SettingsHelper.IsAuthorized = false;
                Debugger.Break();
            };
        }
    }
}
