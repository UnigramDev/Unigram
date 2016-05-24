namespace Telegram.Api.TL
{
    public class TLDecryptedMessageLayer : TLObject
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageLayer;

        public TLInt Layer { get; set; }

        public TLDecryptedMessageBase Message { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Layer.ToBytes(),
                Message.ToBytes());
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Layer = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLDecryptedMessageBase>(bytes, ref position);

            return this;
        }
    }

    public class TLDecryptedMessageLayer17 : TLDecryptedMessageLayer
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageLayer17;

        public TLString RandomBytes { get; set; }

        public TLInt InSeqNo { get; set; }

        public TLInt OutSeqNo { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomBytes.ToBytes(),
                Layer.ToBytes(),
                InSeqNo.ToBytes(),
                OutSeqNo.ToBytes(),
                Message.ToBytes());
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomBytes = GetObject<TLString>(bytes, ref position);
            Layer = GetObject<TLInt>(bytes, ref position);
            InSeqNo = GetObject<TLInt>(bytes, ref position);
            OutSeqNo = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLDecryptedMessageBase>(bytes, ref position);

            return this;
        }
    }
}
