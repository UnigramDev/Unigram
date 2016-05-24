namespace Telegram.Api.TL.Functions.Messages
{
    class TLImportChatInvite : TLObject
    {
        public const string Signature = "#6c50051c";

        public TLString Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes());
        }
    }
}
