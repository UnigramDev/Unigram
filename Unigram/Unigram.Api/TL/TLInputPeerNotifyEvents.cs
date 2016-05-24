namespace Telegram.Api.TL
{
    public abstract class TLInputPeerNotifyEventsBase : TLObject { }

    public class TLInputPeerNotifyEventsEmpty : TLInputPeerNotifyEventsBase
    {
        public const uint Signature = TLConstructors.TLInputPeerNotifyEventsEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputPeerNotifyEventsAll : TLInputPeerNotifyEventsBase
    {
        public const uint Signature = TLConstructors.TLInputPeerNotifyEventsAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
