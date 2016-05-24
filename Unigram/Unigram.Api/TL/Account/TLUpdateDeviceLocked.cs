namespace Telegram.Api.TL.Account
{
    class TLUpdateDeviceLocked : TLObject
    {
        public const string Signature = "#38df3532";

        public TLInt Period { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Period.ToBytes());
        }
    }
}
