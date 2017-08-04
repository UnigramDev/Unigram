using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUpdateBase
    {
        public virtual IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateNewMessage : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateChatParticipantAdd
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChatParticipantDelete
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateNewEncryptedMessage
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateEncryption
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateMessageID
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    //public partial class TLUpdateReadMessages
    //{
    //    public override IList<int> GetPts()
    //    {
    //        return TLUtils.GetPtsRange(Pts, PtsCount);
    //    }
    //}

    public partial class TLUpdateReadMessagesContents : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadHistoryInbox : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("TLUpdateReadHistoryInbox peer={0} max_id={1} pts={2} pts_count={3}", Peer, MaxId, Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadHistoryOutbox : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("TLUpdateReadHistoryOutbox peer={0} max_id={1} pts={2} pts_count={3}", Peer, MaxId, Pts, PtsCount);
        }
    }

    public partial class TLUpdateEncryptedMessagesRead
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateDeleteMessages : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateUserTyping
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChatUserTyping
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateEncryptedChatTyping
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChatParticipants
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateUserStatus
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateUserName
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateUserPhoto
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateContactRegistered
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateContactLink
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    //public partial class TLUpdateActivation
    //{
    //    public override IList<int> GetPts()
    //    {
    //        return new List<int>();
    //    }
    //}

    //public partial class TLUpdateNewAuthorization
    //{
    //    public override IList<int> GetPts()
    //    {
    //        return new List<int>();
    //    }
    //}

    public partial class TLUpdateDCOptions
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateNotifySettings
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateUserBlocked
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdatePrivacy
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateUserPhone
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateServiceNotification
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateWebPage : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateChannelTooLong
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateEditMessage : ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateNewChannelMessage : ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadChannelInbox
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateDeleteChannelMessages : ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateEditChannelMessage : ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }


    public partial class TLUpdateChannelWebPage : ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new List<int>(); //TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateChannelMessageViews
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChannel
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChatAdmins
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateChatParticipantAdmin
    {
        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }
}
