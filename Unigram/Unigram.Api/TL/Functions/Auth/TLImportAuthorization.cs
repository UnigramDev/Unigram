namespace Telegram.Api.TL.Functions.Auth
{
    public class TLImportAuthorization : TLObject
    {
        public const string Signature = "#e3ef9613";

        public TLInt Id { get; set; }

        public TLString Bytes { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Bytes.ToBytes());
        }
    }
}
