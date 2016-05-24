using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
#if LAYER_41
    class TLReadHistory : TLObject
    {
        public const uint Signature = 0xe306d3a;
        
        public TLInputPeerBase Peer { get; set; }

        public TLInt MaxId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                MaxId.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            MaxId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputPeerBase>(input);
            MaxId = GetObject<TLInt>(input);

            return this;
        }
    }
#else
    class TLReadHistory : TLObject
    {
        public const uint Signature = 0xb04f2510;
        
        public TLInputPeerBase Peer { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Offset { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                MaxId.ToBytes(),
                Offset.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            MaxId.ToStream(output);
            Offset.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputPeerBase>(input);
            MaxId = GetObject<TLInt>(input);
            Offset = GetObject<TLInt>(input);

            return this;
        }
    }
#endif
}
