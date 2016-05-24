using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public interface IInviter
    {
        TLInt InviterId { get; set; }

        TLInt Date { get; set; }
    }

    public abstract class TLChatParticipantBase : TLObject
    {
        public TLInt UserId { get; set; }
    }

    public class TLChatParticipant : TLChatParticipantBase, IInviter
    {
        public const uint Signature = TLConstructors.TLChatParticipant;

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

    public class TLChatParticipantCreator : TLChatParticipantBase
    {
        public const uint Signature = TLConstructors.TLChatParticipantCreator;

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

    public class TLChatParticipantAdmin : TLChatParticipantBase, IInviter
    {
        public const uint Signature = TLConstructors.TLChatParticipantAdmin;

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
}
