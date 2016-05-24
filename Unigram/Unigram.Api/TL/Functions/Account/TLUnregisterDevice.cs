namespace Telegram.Api.TL.Account
{
    public class TLUnregisterDevice : TLObject
    {
        public const string Signature = "#65c55b40";

        public TLInt TokenType { get; set; }

        public TLString Token { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                TokenType.ToBytes(),
                Token.ToBytes());
        }
    }
}