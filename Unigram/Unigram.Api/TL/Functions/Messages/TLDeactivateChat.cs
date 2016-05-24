namespace Telegram.Api.TL.Functions.Messages
{
    class TLDeactivateChat : TLObject
    {
        public const uint Signature = 0x626f0b41;

        public TLInt ChatId { get; set; }

        public TLBool Enabled { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                Enabled.ToBytes());
        }
    }
}