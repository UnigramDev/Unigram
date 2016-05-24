namespace Telegram.Api.TL.Functions.Messages
{
    public class TLEditChatPhoto : TLObject
    {
#if LAYER_40
        public const string Signature = "#ca4c79d8";

        public TLInt ChatId { get; set; }

        public TLInputChatPhotoBase Photo { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                Photo.ToBytes());
        }
#else
        public const string Signature = "#d881821d";

        public TLInt ChatId { get; set; }

        public TLInputChatPhotoBase Photo { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                Photo.ToBytes());
        }
#endif
    }
}
