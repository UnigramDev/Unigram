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
    public class Bootstrapper
    {
        private UnigramContainer container;

        public Bootstrapper()
        {
            container = UnigramContainer.Instance;
        }

        public void Configure()
        {
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
            container.ContainerBuilder.RegisterType<UploadFileManager>().As<IUploadFileManager>().SingleInstance();
            container.ContainerBuilder.RegisterType<UploadDocumentFileManager>().As<IUploadDocumentFileManager>().SingleInstance();

            container.ContainerBuilder.RegisterType<LocationService>().As<ILocationService>().SingleInstance();

            // ViewModels
            container.ContainerBuilder.RegisterType<LoginWelcomeViewModel>();
            container.ContainerBuilder.RegisterType<LoginPhoneNumberViewModel>();
            container.ContainerBuilder.RegisterType<LoginPhoneCodeViewModel>();
            container.ContainerBuilder.RegisterType<LoginPasswordViewModel>();
            container.ContainerBuilder.RegisterType<MainViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSendLocationViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<DialogViewModel>().SingleInstance();
            container.ContainerBuilder.RegisterType<UserInfoViewModel>(); // .SingleInstance();
            container.ContainerBuilder.RegisterType<DialogSharedMediaViewModel>(); // .SingleInstance();

            container.Build();

            Initialize();
        }

        private void Initialize()
        {
            Execute.Initialize();

            var cacheService = UnigramContainer.Instance.ResolverType<ICacheService>();
            var protoService = UnigramContainer.Instance.ResolverType<IMTProtoService>();
            var updatesService = UnigramContainer.Instance.ResolverType<IUpdatesService>();
            cacheService.Initialize();
            updatesService.GetCurrentUserId = () => protoService.CurrentUserId;
            updatesService.GetStateAsync = protoService.GetStateCallbackAsync;
            updatesService.GetDHConfigAsync = protoService.GetDHConfigCallbackAsync;
            updatesService.GetDifferenceAsync = protoService.GetDifferenceCallbackAsync;
            updatesService.AcceptEncryptionAsync = protoService.AcceptEncryptionCallbackAsync;
            updatesService.SendEncryptedServiceAsync = protoService.SendEncryptedServiceCallbackAsync;
            updatesService.SetMessageOnTimeAsync = protoService.SetMessageOnTimeAsync;
            //updatesService.RemoveFromQueue = protoService.RemoveFromQueue;
            updatesService.UpdateChannelAsync = protoService.UpdateChannelCallbackAsync;
            updatesService.GetParticipantAsync = protoService.GetParticipantCallbackAsync;
            updatesService.GetFullChatAsync = protoService.GetFullChatCallbackAsync;
            updatesService.LoadStateAndUpdate(() => { });

            protoService.AuthorizationRequired += (s, e) =>
            {
                Debugger.Break();
            };
        }
    }
}
