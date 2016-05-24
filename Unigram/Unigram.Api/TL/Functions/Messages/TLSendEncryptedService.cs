using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    public class TLSendEncryptedService : TLObject, IRandomId
    {
        public const uint Signature = 0x32d439a4;

        public TLInputEncryptedChat Peer { get; set; }

        public TLLong RandomId { get; set; }

        public TLString Data { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                RandomId.ToBytes(),
                Data.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            RandomId.ToStream(output);
            Data.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputEncryptedChat>(input);
            RandomId = GetObject<TLLong>(input);
            Data = GetObject<TLString>(input);

            return this;
        }
    }
}