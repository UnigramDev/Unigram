namespace Telegram.Api.TL.Functions.Messages
{
    class TLExportChatInvite : TLObject
    {
#if LAYER_40
        public const string Signature = "#7d885289";

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
#else
        public const string Signature = "#7d885289";

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
#endif
    }
}
