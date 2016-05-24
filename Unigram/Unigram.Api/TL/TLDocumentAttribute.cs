using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLDocumentAttributeBase  : TLObject { }

    public class TLDocumentAttributeImageSize : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeImageSize;

        public TLInt W { get; set; }

        public TLInt H { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                W.ToBytes(),
                H.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            W.ToStream(output);
            H.ToStream(output);
        }
    }

    public class TLDocumentAttributeAnimated : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeAnimated;
        
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

    public class TLDocumentAttributeSticker : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeSticker;

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

    public class TLDocumentAttributeSticker25 : TLDocumentAttributeSticker
    {
        public new const uint Signature = TLConstructors.TLDocumentAttributeSticker25;

        public TLString Alt { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Alt = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Alt.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Alt = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Alt.ToStream(output);
        }
    }

    public class TLDocumentAttributeSticker29 : TLDocumentAttributeSticker25
    {
        public new const uint Signature = TLConstructors.TLDocumentAttributeSticker29;

        public TLInputStickerSetBase Stickerset { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Alt = GetObject<TLString>(bytes, ref position);
            Stickerset = GetObject<TLInputStickerSetBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Alt.ToBytes(),
                Stickerset.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Alt = GetObject<TLString>(input);
            Stickerset = GetObject<TLInputStickerSetBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Alt.ToStream(output);
            Stickerset.ToStream(output);
        }
    }

    public class TLDocumentAttributeVideo : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeVideo;

        public TLInt Duration { get; set; }

        public TLInt W { get; set; }

        public TLInt H { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Duration = GetObject<TLInt>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
        }
    }

    public class TLDocumentAttributeAudio : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeAudio;

        public TLInt Duration { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Duration = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Duration.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Duration = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Duration.ToStream(output);
        }
    }

    public class TLDocumentAttributeAudio32 : TLDocumentAttributeAudio
    {
        public new const uint Signature = TLConstructors.TLDocumentAttributeAudio32;

        public TLString Title { get; set; }

        public TLString Performer { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Duration = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            Performer = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Duration.ToBytes(),
                Title.ToBytes(),
                Performer.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Duration = GetObject<TLInt>(input);
            Title = GetObject<TLString>(input);
            Performer = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Duration.ToStream(output);
            Title.ToStream(output);
            Performer.ToStream(output);
        }
    }

    public class TLDocumentAttributeFileName : TLDocumentAttributeBase
    {
        public const uint Signature = TLConstructors.TLDocumentAttributeFileName;

        public TLString FileName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            FileName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                FileName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            FileName = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            FileName.ToStream(output);
        }
    }
}
