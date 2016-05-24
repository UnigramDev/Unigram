namespace Telegram.Api.TL.Functions.Messages
{
    class TLInstallStickerSet : TLObject
    {
        public const string Signature = "#7b30c3a6";

        public TLInputStickerSetBase Stickerset { get; set; }

        public TLBool Disabled { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Stickerset.ToBytes(),
                Disabled.ToBytes());
        }
    }
}
