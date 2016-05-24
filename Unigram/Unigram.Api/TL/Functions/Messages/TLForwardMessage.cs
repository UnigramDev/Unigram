using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    public class TLForwardMessage : TLObject, IRandomId
    {
        public const uint Signature = 0x3f3f4f2;

        public TLInputPeerBase Peer { get; set; }

        public TLInt Id { get; set; }

        public TLLong RandomId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Id.ToBytes(),
                RandomId.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            Id.ToStream(output);
            RandomId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputPeerBase>(input);
            Id = GetObject<TLInt>(input);
            RandomId = GetObject<TLLong>(input);

            return this;
        }
    }
}