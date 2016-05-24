namespace Telegram.Api.TL
{
    public class TLNonEncryptedMessage : TLTransportMessageWithIdBase
    {
        public TLLong AuthKeyId { get; set; }
        public TLObject Data { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            AuthKeyId = GetObject<TLLong>(bytes, ref position);
            MessageId = GetObject<TLLong>(bytes, ref position);
            var length = GetObject<TLInt>(bytes, ref position);
            Data = GetObject<TLObject>(bytes, ref position);
            
            return this;
        }

        public override byte[] ToBytes()
        {
            var dataBytes = Data.ToBytes();
            var length = new TLInt(dataBytes.Length);

            return TLUtils.Combine(
                AuthKeyId.ToBytes(),
                MessageId.ToBytes(),
                length.ToBytes(),
                dataBytes);
        }
    }
}
