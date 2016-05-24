using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLNotifyPeerBase : TLObject { }

    public class TLNotifyPeer : TLNotifyPeerBase
    {
        public TLPeerBase Peer { get; set; }

        public const uint Signature = TLConstructors.TLNotifyPeer;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);

            return this;
        }
    }

    public class TLNotifyUsers : TLNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLNotifyUsers;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLNotifyChats : TLNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLNotifyChats;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLNotifyAll : TLNotifyPeerBase
    {
        public const uint Signature = TLConstructors.TLNotifyAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }
}
