using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public interface IChannelInviter
    {
        TLInt InviterId { get; set; }

        TLInt Date { get; set; }
    }

    public abstract class TLChannelParticipantBase : TLObject
    {
        public TLInt UserId { get; set; }
    }

    public class TLChannelParticipant : TLChannelParticipantBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipant;

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(Date.ToBytes());
        }
    }

    public class TLChannelParticipantSelf : TLChannelParticipantBase, IChannelInviter
    {
        public const uint Signature = TLConstructors.TLChannelParticipantSelf;

        public TLInt InviterId { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            InviterId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            InviterId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(InviterId.ToBytes());
            output.Write(Date.ToBytes());
        }
    }

    public class TLChannelParticipantModerator : TLChannelParticipantBase, IChannelInviter
    {
        public const uint Signature = TLConstructors.TLChannelParticipantModerator;

        public TLInt InviterId { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            InviterId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            InviterId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(InviterId.ToBytes());
            output.Write(Date.ToBytes());
        }
    }

    public class TLChannelParticipantEditor : TLChannelParticipantBase, IChannelInviter
    {
        public const uint Signature = TLConstructors.TLChannelParticipantEditor;

        public TLInt InviterId { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            InviterId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            InviterId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(InviterId.ToBytes());
            output.Write(Date.ToBytes());
        }
    }

    public class TLChannelParticipantKicked : TLChannelParticipantBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantKicked;

        public TLInt KickedBy { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            KickedBy = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            KickedBy = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(KickedBy.ToBytes());
            output.Write(Date.ToBytes());
        }
    }

    public class TLChannelParticipantCreator : TLChannelParticipantBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantCreator;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
        }
    }
}
