namespace Telegram.Api.TL.Functions.Contacts
{
    class TLResolveUsername : TLObject
    {
#if LAYER_40
        public const uint Signature = 0xf93ccba3;

        public TLString Username { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Username.ToBytes());
        }
#else
        public const string Signature = "#bf0131c";

        public TLString Username { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Username.ToBytes());
        }
#endif
    }
}
