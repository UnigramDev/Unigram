using System;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void SaveConfig()
        {
            _cacheService.SetConfig(_config);
        }

        public TLConfig LoadConfig()
        {
            throw new NotImplementedException();
        }
    }
}
