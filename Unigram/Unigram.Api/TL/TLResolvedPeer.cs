using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLResolvedPeer : TLObject
    {
        public const uint Signature = TLConstructors.TLResolvedPeer;

        public TLPeerBase Peer { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            Chats = GetObject<TLVector<TLChatBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Peer.ToBytes());
            output.Write(Chats.ToBytes());
            output.Write(Users.ToBytes());
        }
    }
}
