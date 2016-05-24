namespace Telegram.Api.TL.Functions.Messages
{
    class TLEditChatAdmin : TLObject
    {
        public const uint Signature = 0xa9e69f2e;

        public TLInt ChatId { get; set; }

        public TLInputUserBase UserId { get; set; }

        public TLBool IsAdmin { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                UserId.ToBytes(),
                IsAdmin.ToBytes());
        }
    }
}
