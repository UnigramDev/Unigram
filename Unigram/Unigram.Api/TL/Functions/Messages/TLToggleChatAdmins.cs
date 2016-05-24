namespace Telegram.Api.TL.Functions.Messages
{
    class TLToggleChatAdmins : TLObject
    {
        public const uint Signature = 0xec8bd9e1;

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
