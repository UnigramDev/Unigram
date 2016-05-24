using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public enum ChatParticipantsFlags
    {
        Self = 0x1,
    }

    public abstract class TLChatParticipantsBase : TLObject
    {
        public TLInt ChatId { get; set; }
    }

    public class TLChatParticipantsForbidden37 : TLChatParticipantsForbidden
    {
        public new const uint Signature = TLConstructors.TLChatParticipantsForbidden37;

        public TLInt Flags { get; set; }

        public TLChatParticipantBase SelfParticipant { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int) ChatParticipantsFlags.Self))
            {
                SelfParticipant = GetObject<TLChatParticipantBase>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            ChatId = GetObject<TLInt>(input);
            if (IsSet(Flags, (int)ChatParticipantsFlags.Self))
            {
                SelfParticipant = GetObject<TLChatParticipantBase>(input);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ChatId.ToBytes());
            if (IsSet(Flags, (int)ChatParticipantsFlags.Self))
            {
                SelfParticipant.ToStream(output);
            }
        }
    }

    public class TLChatParticipantsForbidden : TLChatParticipantsBase
    {
        public const uint Signature = TLConstructors.TLChatParticipantsForbidden;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ChatId.ToBytes());
        }
    }

    public interface IChatParticipants
    {
        TLInt ChatId { get; set; }

        TLVector<TLChatParticipantBase> Participants { get; set; }

        TLInt Version { get; set; }
    }

    public class TLChatParticipants : TLChatParticipantsBase, IChatParticipants
    {
        public const uint Signature = TLConstructors.TLChatParticipants;

        public TLInt AdminId { get; set; }

        public TLVector<TLChatParticipantBase> Participants { get; set; }

        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            AdminId = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLVector<TLChatParticipantBase>>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            AdminId = GetObject<TLInt>(input);
            Participants = GetObject<TLVector<TLChatParticipantBase>>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ChatId.ToBytes());
            output.Write(AdminId.ToBytes());
            Participants.ToStream(output);
            output.Write(Version.ToBytes());
        }
    }

    public class TLChatParticipants40 : TLChatParticipantsBase, IChatParticipants
    {
        public const uint Signature = TLConstructors.TLChatParticipants40;

        public TLVector<TLChatParticipantBase> Participants { get; set; }

        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLVector<TLChatParticipantBase>>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            Participants = GetObject<TLVector<TLChatParticipantBase>>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ChatId.ToBytes());
            Participants.ToStream(output);
            output.Write(Version.ToBytes());
        }
    }

    public class TLChannelParticipants : TLChatParticipantsBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipants;

        public TLInt Flags { get; set; }

        public TLChatParticipantBase SelfParticipant { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int)ChatParticipantsFlags.Self))
            {
                SelfParticipant = GetObject<TLChatParticipantBase>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            ChatId = GetObject<TLInt>(input);
            if (IsSet(Flags, (int)ChatParticipantsFlags.Self))
            {
                SelfParticipant = GetObject<TLChatParticipantBase>(input);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ChatId.ToBytes());
            if (IsSet(Flags, (int)ChatParticipantsFlags.Self))
            {
                SelfParticipant.ToStream(output);
            }
        }
    }
}
