using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    public class TLReadEncryptedHistory : TLObject
    {
        public const uint Signature = 0x7f4b690a;

        public TLInputEncryptedChat Peer { get; set; }

        public TLInt MaxDate { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                MaxDate.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            MaxDate.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputEncryptedChat>(input);
            MaxDate = GetObject<TLInt>(input);

            return this;
        }
    }
}