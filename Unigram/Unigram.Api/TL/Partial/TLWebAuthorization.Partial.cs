using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public partial class TLWebAuthorization
    {
        private TLUser _bot;
        public TLUser Bot
        {
            get
            {
                if (_bot == null)
                    _bot = InMemoryCacheService.Current.GetUser(BotId) as TLUser;

                return _bot;
            }
        }
    }
}
