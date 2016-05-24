namespace Telegram.Api.TL.Functions.Messages
{
    class TLReceivedMessages : TLObject
    {
        public const string Signature = "#5a954c0";

        public TLInt MaxId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MaxId.ToBytes());
        }
    }
}
