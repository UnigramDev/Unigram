using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUpdatesBase : TLObject
    {
        public abstract IList<int> GetSeq();

        public abstract IList<int> GetPts();
    }

    public partial class TLUpdatesTooLong : TLUpdatesBase
    {
        public override IList<int> GetSeq()
        {
            return new int[0];
        }

        public override IList<int> GetPts()
        {
            return new int[0];
        }
    }

    public partial class TLUpdateShortSentMessage : TLUpdatesBase, ITLMultiPts
    {
        public override IList<int> GetSeq()
        {
            return new int[0];
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            //return string.Format("TLUpdateShortSentMessage id={0} media={1} flags={2}", Id, Media, TLMessageBase.MessageFlagsString(Flags));
            return string.Format("TLUpdateShortSentMessage id={0} media={1} flags={2}", Id, Media, Flags);
        }
    }

    public partial class TLUpdateShortMessage : TLUpdatesBase, ITLMultiPts
    {
        public bool IsUnread { get; set; } = true;

        public override IList<int> GetSeq()
        {
            return new int[0];
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("TLUpdateShortMessage id={0} flags={1} user_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} via_bot_id={8} reply_to_msg_id={9} entities={10}", Id, Flags, UserId, Message.Substring(0, Math.Min(Message.Length, 5)), Pts, PtsCount, Date, FwdFrom, ViaBotId, ReplyToMsgId, Entities);
            //return string.Format("TLUpdateShortMessage id={0} flags={1} user_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} via_bot_id={8} reply_to_msg_id={9} entities={10}", Id, TLMessageBase.MessageFlagsString(Flags), UserId, Message.Substring(0, Math.Min(Message.Length, 5)), Pts, PtsCount, Date, FwdFrom, ViaBotId, ReplyToMsgId, Entities);
        }
    }

    public partial class TLUpdateShortChatMessage : TLUpdatesBase, ITLMultiPts
    {
        public bool IsUnread { get; set; } = true;

        public override IList<int> GetSeq()
        {
            return new int[0];
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("TLUpdateShortChatMessage id={0} flags={1} from_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} reply_to_msg_id={8} via_bot_id={9} entities={10}", Id, Flags, FromId, Message.Substring(0, Math.Min(Message.Length, 5)), Pts, PtsCount, Date, FwdFrom, ViaBotId, ReplyToMsgId, Entities);
            //return string.Format("TLUpdateShortChatMessage id={0} flags={1} from_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} reply_to_msg_id={8} via_bot_id={9} entities={10}", Id, TLMessageBase.MessageFlagsString(Flags), FromId, Message.Substring(0, Math.Min(Message.Length, 5)), Pts, PtsCount, Date, FwdFrom, ViaBotId, ReplyToMsgId, Entities);
        }
    }

    public partial class TLUpdateShort : TLUpdatesBase
    {
        public override IList<int> GetSeq()
        {
            return new int[0];
        }

        public override IList<int> GetPts()
        {
            return Update.GetPts();
        }

        public override string ToString()
        {
            return "TLUpdatesShort update=" + Update;
        }
    }

    public partial class TLUpdates : TLUpdatesBase
    {
        public override IList<int> GetSeq()
        {
            return new List<int> { Seq };
        }

        public override IList<int> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }

        public override string ToString()
        {
            var info = new StringBuilder();
            info.AppendLine("TLUpdates");

            for (var i = 0; i < Updates.Count; i++)
            {
                info.AppendLine(Updates[i].ToString());
            }

            return info.ToString();
        }
    }

    public partial class TLUpdatesCombined : TLUpdatesBase
    {
        public override IList<int> GetSeq()
        {
            var list = new List<int>();

            for (var i = SeqStart; i <= Seq; i++)
            {
                list.Add(i);
            }

            return list;
        }

        public override IList<int> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }
    }
}
