namespace Telegram.Api.TL.Functions.Messages
{
    public class TLSendBroadcast : TLObject
    {
        public const string Signature = "#bf73f4da";

        public TLVector<TLInputUserBase> Contacts { get; set; }

        public TLVector<TLLong> RandomId { get; set; }

        public TLString Message { get; set; }

        public TLInputMediaBase Media { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Contacts.ToBytes(),
                RandomId.ToBytes(),
                Message.ToBytes(),
                Media.ToBytes());
        }
    }
}
