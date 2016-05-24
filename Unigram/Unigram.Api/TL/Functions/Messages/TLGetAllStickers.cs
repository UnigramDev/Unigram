namespace Telegram.Api.TL.Functions.Messages
{
    class TLGetAllStickers : TLObject
    {
        public const string Signature = "#aa3bc868";

        public TLString Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes());
        }
    }
}
