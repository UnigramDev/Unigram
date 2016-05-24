namespace Telegram.Api.TL.Functions.Auth
{
    public class TLExportAuthorization : TLObject
    {
        public const string Signature = "#e5bfffcd";

        public TLInt DCId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                DCId.ToBytes());
        }
    }
}
