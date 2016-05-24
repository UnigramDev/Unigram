namespace Telegram.Api.TL.Functions.Messages
{
    public class TLAddChatUser : TLObject
    {
#if LAYER_40
        public const string Signature = "#f9a0aa09";

        public TLInt ChatId { get; set; }

        public TLInputUserBase UserId { get; set; }

        public TLInt FwdLimit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                UserId.ToBytes(),
                FwdLimit.ToBytes());
        }
#else
        public const string Signature = "#2ee9ee9e";

        public TLInt ChatId { get; set; }

        public TLInputUserBase UserId { get; set; }

        public TLInt FwdLimit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                UserId.ToBytes(),
                FwdLimit.ToBytes());
        }
#endif
    }
}
