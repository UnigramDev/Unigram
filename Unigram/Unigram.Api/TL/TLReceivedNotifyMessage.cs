using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLReceivedNotifyMessage : TLObject
    {
        public const uint Signature = TLConstructors.TLReceivedNotifyMessage;

        public TLInt Id { get; set; }

        public TLInt Flags { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Flags = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Flags.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            Flags = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Flags.ToBytes());
        }
    }
}
