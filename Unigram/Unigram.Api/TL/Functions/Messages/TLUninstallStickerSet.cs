namespace Telegram.Api.TL.Functions.Messages
{
    class TLUninstallStickerSet : TLObject
    {
        public const uint Signature = 0xf96e55de;

        public TLInputStickerSetBase Stickerset { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Stickerset.ToBytes());
        }
    }
}
