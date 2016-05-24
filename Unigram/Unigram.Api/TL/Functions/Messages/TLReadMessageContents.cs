using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    class TLReadMessageContents : TLObject
    {
        public const uint Signature = 0x36a73f77;

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Id.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLVector<TLInt>>(input);

            return this;
        }
    }
}
