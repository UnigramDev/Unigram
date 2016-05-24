namespace Telegram.Api.TL.Functions.Messages
{
    public class TLEditChatTitle : TLObject
    {
#if LAYER_40
        public const string Signature = "#dc452855";

        public TLInt ChatId { get; set; }

        public TLString Title { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                Title.ToBytes());
        }
#else
        public const string Signature = "#b4bc68b5";

        public TLInt ChatId { get; set; }

        public TLString Title { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                Title.ToBytes());
        }
#endif

    }
}
