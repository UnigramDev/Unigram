namespace Telegram.Api.TL.Functions.Messages
{
    class TLCreateChat : TLObject
    {
        public const string Signature = "#419d9aee";

        public TLVector<TLInputUserBase> Users { get; set; }

        public TLString Title { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Users.ToBytes(),
                Title.ToBytes());
        }
    }
}
