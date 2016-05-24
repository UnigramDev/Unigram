using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLStickersBase : TLObject { }

    public class TLStickersNotModified : TLStickersBase
    {
        public const uint Signature = TLConstructors.TLStickersNotModified;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }

    public class TLStickers : TLStickersBase
    {
        public const uint Signature = TLConstructors.TLStickers;

        public TLString Hash { get; set; }

        public TLVector<TLDocumentBase> Stickers { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLString>(bytes, ref position);
            Stickers = GetObject<TLVector<TLDocumentBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes(),
                Stickers.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Hash = GetObject<TLString>(input);
            Stickers = GetObject<TLVector<TLDocumentBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Hash.ToStream(output);
            Stickers.ToStream(output);
        }
    }
}
