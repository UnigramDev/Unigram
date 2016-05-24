using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLMessageGroup : TLObject
    {
        public const uint Signature = TLConstructors.TLMessageGroup;

        public TLInt MinId { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Count { get; set; }

        public TLInt Date { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MinId.ToBytes(),
                MaxId.ToBytes(),
                Count.ToBytes(),
                Date.ToBytes()
            );
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MinId = GetObject<TLInt>(bytes, ref position);
            MaxId = GetObject<TLInt>(bytes, ref position);
            Count = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            MinId.ToStream(output);
            MaxId.ToStream(output);
            Count.ToStream(output);
            Date.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            MinId = GetObject<TLInt>(input);
            MaxId = GetObject<TLInt>(input);
            Count = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }
    }
}
