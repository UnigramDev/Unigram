namespace Telegram.Api.TL
{
    public class TLAffectedHistory : TLObject
    {
        public const uint Signature = TLConstructors.TLAffectedHistory;

        public TLInt Pts { get; set; }

        public TLInt Seq { get; set; }

        public TLInt Offset { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);
            Offset = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }

    public class TLAffectedHistory24 : TLAffectedHistory, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLAffectedHistory24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Offset = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }
}
