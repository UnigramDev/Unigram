namespace Telegram.Api.TL
{
    public class TLRPCResult : TLObject
    {
        public const uint Signature = TLConstructors.TLRPCResult;

        public TLLong RequestMessageId { get; set; }

        public TLObject Object { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RequestMessageId = GetObject<TLLong>(bytes, ref position);
            Object = GetObject<TLObject>(bytes, ref position);

            return this;
        }
    }
}
