using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public abstract partial class TLChannelParticipantBase
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
                return this is TLChannelParticipantCreator;
            }
        }

        //public bool IsMod
        //{
        //    get
        //    {
        //        return this is TLChannelParticipantModerator || IsAdmin;
        //    }
        //}

        public bool IsEditor
        {
            get
            {
                return this is TLChannelParticipantEditor || IsCreator;
            }
        }
    }
}
