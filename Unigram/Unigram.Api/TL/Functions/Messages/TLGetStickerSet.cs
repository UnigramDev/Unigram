namespace Telegram.Api.TL.Functions.Messages
{
    class TLGetStickerSet : TLObject
    {
        public const string Signature = "#2619a90e";

        public TLInputStickerSetBase Stickerset { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Stickerset.ToBytes());
        }
    }
}
