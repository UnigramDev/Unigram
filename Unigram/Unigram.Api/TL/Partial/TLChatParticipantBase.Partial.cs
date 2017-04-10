using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public abstract partial class TLChatParticipantBase
    {
        private TLUser _user;
        public TLUser User
        {
            get
            {
                if (_user == null)
                    _user = InMemoryCacheService.Current.GetUser(UserId) as TLUser;

                return _user;
            }
        }

        public bool IsCreator
        {
            get
            {
                return this is TLChatParticipantCreator;
            }
        }

        public bool IsAdmin
        {
            get
            {
                return this is TLChatParticipantAdmin || IsCreator;
            }
        }
    }
}
