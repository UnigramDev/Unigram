namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLGetContacts : TLObject
    {
        public const string Signature = "#22c6aa08";

        public TLString Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes());
        }
    }
}