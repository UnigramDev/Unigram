namespace Telegram.Api.TL.Functions.Messages
{
    class TLCheckChatInvite : TLObject
    {
        public const uint Signature = 0x3eadb1bb;

        public TLString Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes());
        }
    }
}
