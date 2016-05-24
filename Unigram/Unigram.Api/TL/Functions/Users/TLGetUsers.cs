namespace Telegram.Api.TL.Functions.Users
{
    public class TLGetUsers : TLObject
    {
        public const string Signature = "#d91a548";

        public TLVector<TLInputUserBase> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
