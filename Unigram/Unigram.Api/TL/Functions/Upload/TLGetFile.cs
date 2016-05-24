namespace Telegram.Api.TL.Functions.Upload
{
    public class TLGetFile : TLObject
    {
        public const string Signature = "#e3a6cfb5";

        public TLInputFileLocationBase Location { get; set; }

        public TLInt Offset { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Location.ToBytes(),
                Offset.ToBytes(),
                Limit.ToBytes());
        }
    }
}
