namespace Telegram.Api.TL.Functions.Auth
{
    public class TLSendInvites : TLObject
    {
        public const string Signature = "#771c1d97";

        public TLVector<TLString> PhoneNumbers { get; set; }

        public TLString Message { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumbers.ToBytes(),
                Message.ToBytes());
        }
    }
}
