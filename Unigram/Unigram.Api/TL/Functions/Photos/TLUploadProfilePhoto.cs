namespace Telegram.Api.TL.Functions.Photos
{
    public class TLUploadProfilePhoto : TLObject
    {
        public const string Signature = "#d50f9c88";

        public TLInputFile File { get; set; }

        public TLString Caption { get; set; }

        public TLInputGeoPointBase GeoPoint { get; set; }

        public TLInputPhotoCropBase Crop { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Caption.ToBytes(),
                GeoPoint.ToBytes(),
                Crop.ToBytes());
        }
    }
}
