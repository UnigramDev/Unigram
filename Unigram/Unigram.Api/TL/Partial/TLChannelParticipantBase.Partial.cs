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

        public bool IsAdmin
        {
            get
            {
                return this is TLChannelParticipantAdmin || IsCreator;
            }
        }
    }

    public partial class TLChannelParticipantAdmin
    {
        private TLUser _promotedByUser;
        public TLUser PromotedByUser
        {
            get
            {
                if (_promotedByUser == null)
                    _promotedByUser = InMemoryCacheService.Current.GetUser(PromotedBy) as TLUser;

                return _promotedByUser;
            }
        }
    }

    public partial class TLChannelParticipantBanned
    {
        private TLUser _kickedByUser;
        public TLUser KickedByUser
        {
            get
            {
                if (_kickedByUser == null)
                    _kickedByUser = InMemoryCacheService.Current.GetUser(KickedBy) as TLUser;

                return _kickedByUser;
            }
        }
    }
}
