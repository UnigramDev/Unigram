using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputChannelBase : TLObject { }

    public class TLInputChannelEmpty : TLInputChannelBase
    {
        public const uint Signature = TLConstructors.TLInputChannelEmpty;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
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

    public class TLInputChannel : TLInputChannelBase
    {
        public const uint Signature = TLConstructors.TLInputChannel;

        public TLInt ChannelId { get; set; }

        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChannelId.ToBytes(),
                AccessHash.ToBytes()
            );
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            ChannelId.ToStream(output);
            AccessHash.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);

            return this;
        }
    }
}
