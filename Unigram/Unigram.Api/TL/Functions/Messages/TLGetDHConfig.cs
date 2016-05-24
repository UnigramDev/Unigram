namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetDHConfig : TLObject
    {
        public const string Signature = "#26cf8950";

        public TLInt Version { get; set; }

        public TLInt RandomLength { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Version.ToBytes(),
                RandomLength.ToBytes());
        }
    }
}
