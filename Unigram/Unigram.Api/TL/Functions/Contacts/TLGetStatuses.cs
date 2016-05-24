namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLGetStatuses : TLObject
    {
        public const string Signature = "#c4a353ee";

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
