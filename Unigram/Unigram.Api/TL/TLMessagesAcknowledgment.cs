namespace Telegram.Api.TL
{
    public class TLMessagesAcknowledgment : TLObject
    {
        public const uint Signature = TLConstructors.TLMessagesAcknowledgment; 

        public TLVector<TLLong> MessageIds { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MessageIds = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }
    }
}