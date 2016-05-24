namespace Telegram.Api.TL
{
    public class TLAccountDaysTTL : TLObject
    {
        public const uint Signature = TLConstructors.TLAccountDaysTTL;

        public TLInt Days { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Days = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Days.ToBytes());
        }
    }
}
