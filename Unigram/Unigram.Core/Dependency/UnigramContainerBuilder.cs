namespace Unigram.Core.Dependency
{
    using Autofac;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class UnigramContainer
    {
        private static UnigramContainer instance = new UnigramContainer();
        private ContainerBuilder builder = new ContainerBuilder();
        private IContainer container;

        private UnigramContainer()
        {

        }

        public static UnigramContainer Instance
        {
            get
            {
                return instance;
            }
        }

        public ContainerBuilder ContainerBuilder
        {
            get
            {
                return builder;
            }
        }

        public void Build()
        {
            container = builder.Build();
        }

        public TService ResolverType<TService>()
        {
            TService result = default(TService);
            if (container != null)
            {
                result = container.Resolve<TService>();
            }

            return result;
        }
    }
}
