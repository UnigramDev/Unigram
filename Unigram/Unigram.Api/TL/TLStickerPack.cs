using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLStickerPack : TLObject
    {
        public const uint Signature = TLConstructors.TLStickerPack;

        public TLString Emoticon { get; set; }

        public TLVector<TLLong> Documents { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Emoticon = GetObject<TLString>(bytes, ref position);
            Documents = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Emoticon.ToBytes(),
                Documents.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Emoticon = GetObject<TLString>(input);
            Documents = GetObject<TLVector<TLLong>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Emoticon.ToStream(output);
            Documents.ToStream(output);
        }
    }
}
