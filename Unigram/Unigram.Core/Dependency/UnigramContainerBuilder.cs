namespace Unigram.Core.Dependency
{
    using Autofac;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class UnigramContainerBuilder : ContainerBuilder
    {
        private static UnigramContainerBuilder instance = new UnigramContainerBuilder();

        private UnigramContainerBuilder()
        {
        }

        public static UnigramContainerBuilder Instance
        {
            get
            {
                return instance;
            }
        }
    }
}
