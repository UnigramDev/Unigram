namespace Telegram.Api.TL
{
    public class TLContactFound : TLObject
    {
        public const uint Signature = TLConstructors.TLContactFound;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                UserId.ToBytes());
        }
    }
}
