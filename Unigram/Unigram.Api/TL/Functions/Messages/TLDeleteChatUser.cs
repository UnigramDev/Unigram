namespace Telegram.Api.TL.Functions.Messages
{
    public class TLDeleteChatUser : TLObject
    {
#if LAYER_40
        public const string Signature = "#e0611f16";

        public TLInt ChatId { get; set; }

        public TLInputUserBase UserId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                UserId.ToBytes());
        }
#else
        public const string Signature = "#c3c5cd23";

        public TLInt ChatId { get; set; }

        public TLInputUserBase UserId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                UserId.ToBytes());
        }
#endif
    }
}
