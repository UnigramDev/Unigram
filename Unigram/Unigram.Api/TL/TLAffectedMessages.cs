namespace Telegram.Api.TL
{
    public class TLAffectedMessages : TLObject, IMultiPts
    {
        public const uint Signature = TLConstructors.TLAffectedMessages;

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }
}
