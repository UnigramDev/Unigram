using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLRecentlyUsedSticker : TLObject
    {
        public const uint Signature = TLConstructors.TLRecentlyUsedSticker;

        public TLLong Id { get; set; }
        public TLLong Count { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Count = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Count.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            Count = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            Count.ToStream(output);
        }

        public override string ToString()
        {
            return string.Format("Id={0} Count={1}", Id, Count);
        }
    }
}
