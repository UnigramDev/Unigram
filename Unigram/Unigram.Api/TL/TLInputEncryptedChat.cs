using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLInputEncryptedChat : TLObject
    {
        public const uint Signature = TLConstructors.TLInputEncryptedChat;

        public TLInt ChatId { get; set; }

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes(),
                AccessHash.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            ChatId.ToStream(output);
            AccessHash.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);

            return this;
        }
    }
}
