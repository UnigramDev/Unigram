using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [Flags]
    public enum ChannelDifferenceFlags
    {
        Final = 0x1,
        Timeout = 0x2
    }

    public abstract class TLChannelDifferenceBase : TLObject
    {
        public TLInt Flags { get; set; }

        public TLInt Pts { get; set; }

        public TLInt Timeout { get; set; }
    }

    public class TLChannelDifferenceEmpty : TLChannelDifferenceBase
    {
        public const uint Signature = TLConstructors.TLChannelDifferenceEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int) ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(bytes, ref position);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Pts.ToStream(output);
            if (IsSet(Flags, (int) ChannelDifferenceFlags.Timeout))
            {
                Timeout.ToStream(output);
            }
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Pts = GetObject<TLInt>(input);
            if (IsSet(Flags, (int) ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(input);
            }

            return this;
        }

        public override string ToString()
        {
            return string.Format("TLChannelDifferenceEmpty flags={0} pts={1} timeout={2}", Flags, Pts, Timeout);
        }
    }
    public class TLChannelDifferenceTooLong : TLChannelDifferenceBase
    {
        public const uint Signature = TLConstructors.TLChannelDifferenceTooLong;

        public TLInt TopMessage { get; set; }

        public TLInt TopImportantMessage { get; set; }

        public TLInt ReadInboxMaxId { get; set; }

        public TLInt UnreadCount { get; set; }

        public TLInt UnreadImportantCount { get; set; }

        public TLVector<TLMessageBase> Messages { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(bytes, ref position);
            }
            TopMessage = GetObject<TLInt>(bytes, ref position);
            TopImportantMessage = GetObject<TLInt>(bytes, ref position);
            ReadInboxMaxId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            UnreadImportantCount = GetObject<TLInt>(bytes, ref position);
            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Pts.ToStream(output);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout.ToStream(output);
            }
            TopMessage.ToStream(output);
            TopImportantMessage.ToStream(output);
            ReadInboxMaxId.ToStream(output);
            UnreadCount.ToStream(output);
            UnreadImportantCount.ToStream(output);
            Messages.ToStream(output);
            Chats.ToStream(output);
            Users.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Pts = GetObject<TLInt>(input);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(input);
            }
            TopMessage = GetObject<TLInt>(input);
            TopImportantMessage = GetObject<TLInt>(input);
            ReadInboxMaxId = GetObject<TLInt>(input);
            UnreadCount = GetObject<TLInt>(input);
            UnreadImportantCount = GetObject<TLInt>(input);
            Messages = GetObject<TLVector<TLMessageBase>>(input);
            Chats = GetObject<TLVector<TLChatBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("TLChannelDifferenceTooLong flags={0} pts={1} timeout={2} new_messages={3}", Flags, Pts, Timeout, Messages.Count);
        }
    }

    public class TLChannelDifference : TLChannelDifferenceBase
    {
        public const uint Signature = TLConstructors.TLChannelDifference;

        public TLVector<TLMessageBase> NewMessages { get; set; }

        public TLVector<TLUpdateBase> OtherUpdates { get; set; }

        public TLVector<TLChatBase> Chats { get; set; } 

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(bytes, ref position);
            }
            NewMessages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Pts.ToStream(output);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout.ToStream(output);
            }
            NewMessages.ToStream(output);
            OtherUpdates.ToStream(output);
            Chats.ToStream(output);
            Users.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Pts = GetObject<TLInt>(input);
            if (IsSet(Flags, (int)ChannelDifferenceFlags.Timeout))
            {
                Timeout = GetObject<TLInt>(input);
            }
            NewMessages = GetObject<TLVector<TLMessageBase>>(input);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(input);
            Chats = GetObject<TLVector<TLChatBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("TLChannelDifference flags={0} pts={1} timeout={2} new_messages={3} other_updates={4}", Flags, Pts, Timeout, NewMessages.Count, OtherUpdates.Count);
        }
    }
}
