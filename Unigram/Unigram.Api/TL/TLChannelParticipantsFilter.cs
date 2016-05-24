using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLChannelParticipantsFilterBase : TLObject { }

    public class TLChannelParticipantsRecent : TLChannelParticipantsFilterBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantsRecent;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLChannelParticipantsAdmins : TLChannelParticipantsFilterBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantsAdmins;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLChannelParticipantsKicked : TLChannelParticipantsFilterBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantsKicked;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLChannelParticipantsBots : TLChannelParticipantsFilterBase
    {
        public const uint Signature = TLConstructors.TLChannelParticipantsBots;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }
}
