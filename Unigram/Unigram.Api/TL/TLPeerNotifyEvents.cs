using System.Runtime.Serialization;

namespace Telegram.Api.TL
{
    [KnownType(typeof(TLPeerNotifyEventsEmpty))]
    [KnownType(typeof(TLPeerNotifyEventsAll))]
    [DataContract]
    public abstract class TLPeerNotifyEventsBase : TLObject { }

    [DataContract]
    public class TLPeerNotifyEventsEmpty : TLPeerNotifyEventsBase
    {
        public const uint Signature = TLConstructors.TLPeerNotifyEventsEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    [DataContract]
    public class TLPeerNotifyEventsAll : TLPeerNotifyEventsBase
    {
        public const uint Signature = TLConstructors.TLPeerNotifyEventsAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }
}
