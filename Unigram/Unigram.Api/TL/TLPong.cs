namespace Telegram.Api.TL
{
    public class TLPing : TLObject
    {
        public const uint Signature = TLConstructors.TLPing;

        public TLLong PingId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PingId.ToBytes());
        }
    }

    public class TLPingDelayDisconnect : TLObject
    {
        public const uint Signature = TLConstructors.TLPingDelayDisconnect;

        public TLLong PingId { get; set; }

        public TLInt DisconnectDelay { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PingId.ToBytes(),
                DisconnectDelay.ToBytes());
        }
    }

    public class TLPong : TLObject
    {
        public const uint Signature = TLConstructors.TLPong;

        public TLLong MessageId { get; set; }

        public TLLong PingId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MessageId = GetObject<TLLong>(bytes, ref position);
            PingId = GetObject<TLLong>(bytes, ref position);

            return this;
        }
    }
}
