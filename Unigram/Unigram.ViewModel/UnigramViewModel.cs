namespace Unigram.ViewModel
{
    using GalaSoft.MvvmLight;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Telegram.Api.Services;
    using Telegram.Api.Services.Cache;

    /// <summary>
    /// Base class for ViewModel
    /// </summary>
    public class UnigramViewModel : ViewModelBase
    {
        private IMTProtoService mtProtoService;
        private ICacheService cacheService;

        public UnigramViewModel(IMTProtoService mtProtoService, ICacheService cacheService)
        {
            this.mtProtoService = mtProtoService;
            this.cacheService = cacheService;
        }

        /// <summary>
        /// Gets a reference to the <see cref="Telegram.Api.Services.IMTProtoService"/> 
        /// class that handle API requests
        /// </summary>
        protected IMTProtoService MTProtoService
        {
            get
            {
                return mtProtoService;
            }
        }

        /// <summary>
        /// Gets a refernce to the <see cref="Telegram.Api.Services.Cache.ICacheService"/> 
        /// class that represente in memory cache
        /// </summary>
        protected ICacheService CacheService
        {
            get
            {
                return cacheService;
            }
        }
    }
}
