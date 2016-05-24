using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLChannelsChannelParticipants : TLObject
    {
        public const uint Signature = TLConstructors.TLChannelsChannelParticipants;

        public TLInt Count { get; set; }

        public TLVector<TLChannelParticipantBase> Participants { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Count = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLVector<TLChannelParticipantBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Count = GetObject<TLInt>(input);
            Participants = GetObject<TLVector<TLChannelParticipantBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Count.ToBytes());
            output.Write(Participants.ToBytes());
            output.Write(Users.ToBytes());
        }
    }

    public class TLChannelsChannelParticipant : TLObject
    {
        public const uint Signature = TLConstructors.TLChannelsChannelParticipant;

        public TLChannelParticipantBase Participant { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Participant = GetObject<TLChannelParticipantBase>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Participant = GetObject<TLChannelParticipantBase>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Participant.ToBytes());
            output.Write(Users.ToBytes());
        }
    }
}
