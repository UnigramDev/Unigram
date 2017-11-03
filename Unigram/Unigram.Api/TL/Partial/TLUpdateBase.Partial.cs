using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUpdateBase
    {
        //public virtual IList<int> GetPts()
        //{
        //    return new int[0];
        //}

        public abstract IList<int> GetPts();
    }

    public partial class TLUpdateNewMessage : TLUpdateBase, ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateChatParticipantAdd : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChatParticipantDelete : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateNewEncryptedMessage : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateEncryption : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateMessageID : TLUpdateBase
    {
        public override string ToString()
        {
            return string.Format("TLUpdateMessageId id={0} random_id={1}", Id, RandomId);
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateReadMessagesContents : TLUpdateBase, ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadHistoryInbox : TLUpdateBase, ITLMultiPts
    {
        public override string ToString()
        {
            return string.Format("TLUpdateReadHistoryInbox peer={0} max_id={1} pts={2} pts_count={3}", Peer, MaxId, Pts, PtsCount);
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadHistoryOutbox : TLUpdateBase, ITLMultiPts
    {
        public override string ToString()
        {
            return string.Format("TLUpdateReadHistoryOutbox peer={0} max_id={1} pts={2} pts_count={3}", Peer, MaxId, Pts, PtsCount);
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateEncryptedMessagesRead : TLUpdateBase
    {
        public override string ToString()
        {
            return string.Format("{0} chat_id={1} max_date={2} date={3}", GetType().Name, ChatId, MaxDate, Date);
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateDeleteMessages : TLUpdateBase, ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateUserTyping : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChatUserTyping : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateEncryptedChatTyping : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChatParticipants : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateUserStatus : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateUserName : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateUserPhoto : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateContactRegistered : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateContactLink : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateNewAuthorization : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateDCOptions : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateNotifySettings : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateUserBlocked : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdatePrivacy : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateUserPhone : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateServiceNotification : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateWebPage : TLUpdateBase, ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateChannelTooLong : TLUpdateBase
    {
        public override string ToString()
        {
            return string.Format("TLUpdateChannelTooLong channel_id={0} pts={1} flags={2}", ChannelId, Pts, Flags);
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelGroup : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateNewChannelMessage : TLUpdateBase, ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateReadChannelInbox : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateDeleteChannelMessages : TLUpdateBase, ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelMessageViews : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannel : TLUpdateBase
    {
        public override string ToString()
        {
            return "TLUpdateChannel channel_id=" + ChannelId;
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChatAdmins : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChatParticipantAdmin : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateNewStickerSet : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateStickerSetsOrder : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateStickerSets : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateSavedGifs : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotInlineQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotInlineSend : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateEditChannelMessage : TLUpdateBase, ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelPinnedMessage : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotCallbackQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateInlineBotCallbackQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateEditMessage : TLUpdateBase, ITLMultiPts
    {
        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateReadChannelOutbox : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateDraftMessage : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateReadFeaturedStickers : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateRecentStickers : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateConfig : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdatePtsChanged : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelWebPage : TLUpdateBase, ITLMultiChannelPts
    {
        public override IList<int> GetPts()
        {
            return new int[0]; //TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdatePhoneCall : TLUpdateBase
    {
        public override string ToString()
        {
            return string.Format("TLUpdatePhoneCall PhoneCall={0}", PhoneCall);
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateDialogPinned : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdatePinnedDialogs : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotWebhookJSON : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotWebhookJSONQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotShippingQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateBotPrecheckoutQuery : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateLangPackTooLong : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateLangPack : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateFavedStickers : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateContactsReset : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelReadMessagesContents : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateChannelAvailableMessages : TLUpdateBase
    {
        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }
}
