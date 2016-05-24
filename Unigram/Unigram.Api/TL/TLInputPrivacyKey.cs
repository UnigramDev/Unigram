namespace Telegram.Api.TL
{
    public abstract class TLInputPrivacyKeyBase : TLObject { }

    public class TLInputPrivacyKeyStatusTimestamp : TLInputPrivacyKeyBase
    {
        public const uint Signature = TLConstructors.TLInputPrivacyKeyStatusTimestamp;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
