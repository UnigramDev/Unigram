namespace Unigram.ViewModel
{
    using Telegram.Api.Services;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Telegram.Api.Services.Cache;

    public class LoginPhoneNumberViewModel : UnigramViewModel
    {
        public LoginPhoneNumberViewModel(IMTProtoService mtProtoService, ICacheService cacheService)
            : base(mtProtoService, cacheService)
        {

        }
    }
}
