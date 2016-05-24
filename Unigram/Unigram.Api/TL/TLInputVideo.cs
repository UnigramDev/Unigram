namespace Telegram.Api.TL
{
    public abstract class TLInputVideoBase : TLObject { }

    public class TLInputVideoEmpty : TLInputVideoBase
    {
        public const uint Signature = TLConstructors.TLInputVideoEmpty;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputVideo : TLInputVideoBase
    {
        public const uint Signature = TLConstructors.TLInputVideo;

        public TLLong Id { get; set; }
        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }
    }
}
