namespace Telegram.Api.TL.Functions.Stuff
{
    class TLMessageAcknowledgments : TLObject
    {
        public const string Signature = "#62d6b459";

        public TLVector<TLLong> MsgIds { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MsgIds.ToBytes());
        }
    }
}
