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

    public class Bootstrapper
    {
        private UnigramContainerBuilder container;

        public Bootstrapper()
        {
            container = UnigramContainerBuilder.Instance;
        }

        public void Configure()
        {
            container.RegisterType<MTProtoService>().As<IMTProtoService>();
            container.RegisterType<InMemoryCacheService>().As<ICacheService>();
            container.RegisterType<PhoneInfoService>().As<IDeviceInfoService>();
            container.RegisterType<UpdatesService>().As<IUpdatesService>();
            container.RegisterType<TransportService>().As<ITransportService>();
            container.RegisterType<ConnectionService>().As<IConnectionService>();
        }
    }
}
