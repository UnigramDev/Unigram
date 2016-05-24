using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Telegram.Api.TL
{
    public abstract class TLUpdatesBase : TLObject
    {
        public abstract IList<TLInt> GetSeq();

        public abstract IList<TLInt> GetPts();
    }

    public class TLUpdatesTooLong : TLUpdatesBase
    {
        public const uint Signature = TLConstructors.TLUpdatesTooLong;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdatesShortSentMessage : TLUpdatesBase, ISentMessageMedia, IMultiPts
    {
        public const uint Signature = TLConstructors.TLUpdatesShortSentMessage;

        public TLInt Flags { get; set; }

        public TLInt Id { get; set; }

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }

        public TLInt Date { get; set; }

        public TLMessageMediaBase Media { get; set; }

        public TLVector<TLMessageEntityBase> Entities { get; set; }

        public bool HasMedia { get { return IsSet(Flags, (int) MessageFlags.Media); } }

        public bool HasEntities { get { return IsSet(Flags, (int) MessageFlags.Entities); } }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Media))
            {
                Media = GetObject<TLMessageMediaBase>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("SentMessage: Id: {0} Media: {1}", Id, Media);
        }
    }

    public class TLUpdatesShortMessage : TLUpdatesBase
    {
        public const uint Signature = TLConstructors.TLUpdateShortMessage;

        public TLInt Id { get; set; }

        public TLInt UserId { get; set; }

        public TLString Message { get; set; }

        public TLInt Pts { get; set; }

        public TLInt Date { get; set; }

        public TLInt Seq { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt> { Seq };
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt> { Pts };
        }

        public override string ToString()
        {
            return string.Format("UserMessage: FromId: {0} Message: {1}", UserId, Message);
        }
    }

    public class TLUpdatesShortMessage24 : TLUpdatesShortMessage, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateShortMessage24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("UserMessage: FromId: {0} Message: {1}", UserId, Message);
        }
    }

    public class TLUpdatesShortMessage25 : TLUpdatesShortMessage24
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortMessage25;

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public TLInt FwdFromId { get; set; }

        public TLInt FwdDate { get; set; }

        public TLInt ReplyToMsgId { get; set; }

        public TLBool Out
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Out)); }
        }

        public TLBool Unread
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Unread)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Unread);
                }
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("UserMessage: FromId: {0} Message: {1}", UserId, Message);
        }
    }

    public class TLUpdatesShortMessage34 : TLUpdatesShortMessage25
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortMessage34;

        public TLVector<TLMessageEntityBase> Entities { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("UserMessage: FromId: {0} Message: {1}", UserId, Message);
        }
    }

    public class TLUpdatesShortMessage40 : TLUpdatesShortMessage34
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortMessage40;

        public TLPeerBase FwdFrom { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFrom = GetObject<TLPeerBase>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

#if DEBUG
            var messageString = Message.ToString();
            var logString = string.Format("TLUpdateShortMessage40 id={0} flags={1} user_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} fwd_date={8} reply_to_msg_id={9} entities={10}", Id, TLMessageBase.MessageFlagsString(Flags), UserId, messageString.Substring(0, Math.Min(messageString.Length, 5)), Pts, PtsCount, Date, FwdFrom, FwdDate, ReplyToMsgId, Entities);

            Logs.Log.Write(logString);
#endif

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("UserMessage: FromId: {0} Message: {1}", UserId, Message);
        }
    }


    public class TLUpdatesShortChatMessage : TLUpdatesShortMessage
    {
        public new const uint Signature = TLConstructors.TLUpdateShortChatMessage;

        public TLInt ChatId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            return string.Format("ChatMessage: ChatId: {0} FromId: {1} Message: {2}", ChatId, UserId, Message);
        }
    }

    public class TLUpdatesShortChatMessage24 : TLUpdatesShortChatMessage, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateShortChatMessage24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("ChatMessage: ChatId: {0} FromId: {1} Message: {2}", ChatId, UserId, Message);
        }
    }

    public class TLUpdatesShortChatMessage25 : TLUpdatesShortChatMessage24
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortChatMessage25;

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public TLInt FwdFromId { get; set; }

        public TLInt FwdDate { get; set; }

        public TLInt ReplyToMsgId { get; set; }

        public TLBool Out
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Out)); }
        }

        public TLBool Unread
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Unread)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Unread);
                }
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }

        public override string ToString()
        {
            return string.Format("ChatMessage: ChatId: {0} FromId: {1} Message: {2}", ChatId, UserId, Message);
        }
    }

    public class TLUpdatesShortChatMessage34 : TLUpdatesShortChatMessage25
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortChatMessage34;

        public TLVector<TLMessageEntityBase> Entities { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            return this;
        }
    }

    public class TLUpdatesShortChatMessage40 : TLUpdatesShortChatMessage34
    {
        public new const uint Signature = TLConstructors.TLUpdatesShortChatMessage40;

        public TLPeerBase FwdFrom { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFrom = GetObject<TLPeerBase>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }



#if DEBUG
            var messageString = Message.ToString();
            var logString = string.Format("TLUpdateShortChatMessage40 id={0} flags={1} user_id={2} message={3} pts={4} pts_count={5} date={6} fwd_from={7} fwd_date={8} reply_to_msg_id={9} entities={10}", Id, TLMessageBase.MessageFlagsString(Flags), UserId, messageString.Substring(0, Math.Min(messageString.Length, 5)), Pts, PtsCount, Date, FwdFrom, FwdDate, ReplyToMsgId, Entities);

            Logs.Log.Write(logString);
#endif

            return this;
        }
    }

    public class TLUpdatesShort : TLUpdatesBase
    {
        public const uint Signature = TLConstructors.TLUpdateShort;

        public TLUpdateBase Update { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Update = GetObject<TLUpdateBase>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>();
        }

        public override IList<TLInt> GetPts()
        {
            return Update.GetPts();
        }

        public override string ToString()
        {
            return "TLUpdatesShort Update: " + Update;
        }
    }

    public class TLUpdates : TLUpdatesBase
    {
        public const uint Signature = TLConstructors.TLUpdates;

        public TLVector<TLUpdateBase> Updates { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLInt Date { get; set; }

        public TLInt Seq { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Updates = GetObject<TLVector<TLUpdateBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
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

        public override IList<TLInt> GetSeq()
        {
            return new List<TLInt>{Seq};
        }

        public override IList<TLInt> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }
    }

    public class TLUpdatesCombined : TLUpdates
    {
        public new const uint Signature = TLConstructors.TLUpdatesCombined;

        public TLInt SeqStart { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Updates = GetObject<TLVector<TLUpdateBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            SeqStart = GetObject<TLInt>(bytes, ref position);               // seq младший
            Seq = GetObject<TLInt>(bytes, ref position);                    // seq старший

            return this;
        }

        public override IList<TLInt> GetSeq()
        {
            var list = new List<TLInt>();

            for (var i = SeqStart.Value; i <= Seq.Value; i++)
            {
                list.Add(new TLInt(i));
            }

            return list;
        }

        public override IList<TLInt> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }
    }
}