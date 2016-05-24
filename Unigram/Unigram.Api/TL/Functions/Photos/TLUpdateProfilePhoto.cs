namespace Telegram.Api.TL.Functions.Photos
{
    public class TLUpdateProfilePhoto : TLObject
    {
        public const string Signature = "#eef579a0";

        public TLInputPhotoBase Id { get; set; }

        public TLInputPhotoCropBase Crop { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Crop.ToBytes());
        }
    }
}
