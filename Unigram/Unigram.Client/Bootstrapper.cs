namespace Unigram.Client
{
    using Core.Dependency;
    using Autofac;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Telegram.Api.Services;
    using Telegram.Api.Services.Cache;
    using Core.Services;
    using Telegram.Api.Services.DeviceInfo;
    using Telegram.Api.Aggregator;
    using Telegram.Api.Services.Connection;
    using Telegram.Api.Services.Updates;
    using Telegram.Api.Transport;
    using ViewModel;
    public class Bootstrapper
    {
        private UnigramContainer container;

        public Bootstrapper()
        {
            container = UnigramContainer.Instance;
        }

        public void Configure()
        {
            container.ContainerBuilder.RegisterType<MTProtoService>().As<IMTProtoService>();
            container.ContainerBuilder.RegisterType<InMemoryCacheService>().As<ICacheService>();
            container.ContainerBuilder.RegisterType<PhoneInfoService>().As<IDeviceInfoService>();
            container.ContainerBuilder.RegisterType<UpdatesService>().As<IUpdatesService>();
            container.ContainerBuilder.RegisterType<TransportService>().As<ITransportService>();
            container.ContainerBuilder.RegisterType<ConnectionService>().As<IConnectionService>();
            container.ContainerBuilder.RegisterType<LoginPhoneNumberViewModel>();

            container.Build();
        }
    }
}
