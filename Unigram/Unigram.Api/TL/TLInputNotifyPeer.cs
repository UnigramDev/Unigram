namespace Telegram.Api.TL
{
    public abstract class TLInputNotifyPeerBase: TLObject { }

    public class TLInputNotifyPeer : TLInputNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLInputNotifyPeer;

        public TLInputPeerBase Peer { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLInputPeerBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes());
        }

        public override string ToString()
        {
            return "inputNotifyPeer " + Peer;
        }
    }

    public class TLInputNotifyUsers : TLInputNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLInputNotifyUsers;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override string ToString()
        {
            return "inputNotifyUsers";
        }
    }

    public class TLInputNotifyChats : TLInputNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLInputNotifyChats;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override string ToString()
        {
            return "inputNotifyChats";
        }
    }

    public class TLInputNotifyAll : TLInputNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLInputNotifyAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override string ToString()
        {
            return "inputNotifyAll";
        }
    }
}
