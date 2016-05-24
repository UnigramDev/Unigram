namespace Telegram.Api.TL.Functions.Messages
{
    class TLMigrateChat : TLObject
    {
        public const uint Signature = 0x15a3b8e3;

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
    }
}
