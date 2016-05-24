using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLMessagesStickerSet : TLObject
    {
        public const uint Signature = TLConstructors.TLMessagesStickerSet;

        public TLStickerSetBase Set { get; set; }
        public TLVector<TLStickerPack> Packs { get; set; }
        public TLVector<TLDocumentBase> Documents { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Set = GetObject<TLStickerSetBase>(bytes, ref position);
            Packs = GetObject<TLVector<TLStickerPack>>(bytes, ref position);
            Documents = GetObject<TLVector<TLDocumentBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Set.ToBytes(),
                Packs.ToBytes(),
                Documents.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Set = GetObject<TLStickerSetBase>(input);
            Packs = GetObject<TLVector<TLStickerPack>>(input);
            Documents = GetObject<TLVector<TLDocumentBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Set.ToStream(output);
            Packs.ToStream(output);
            Documents.ToStream(output);
        }
    }
}
