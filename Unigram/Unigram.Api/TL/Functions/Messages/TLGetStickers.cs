namespace Telegram.Api.TL.Functions.Messages
{
    class TLGetStickers : TLObject
    {
        public const string Signature = "#ae22e045";

        public TLString Emoticon { get; set; }

        public TLString Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Emoticon.ToBytes(),
                Hash.ToBytes());
        }
    }
}
