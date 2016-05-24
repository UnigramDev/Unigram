using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLDocumentBase : TLObject
    {
        public TLLong Id { get; set; }

        public virtual int DocumentSize { get { return 0; } }

        public static bool DocumentEquals(TLDocumentBase document1, TLDocumentBase document2)
        {
            var doc1 = document1 as TLDocument;
            var doc2 = document2 as TLDocument;

            if (doc1 == null || doc2 == null) return false;

            return doc1.Id.Value == doc2.Id.Value
                   && doc1.DCId.Value == doc2.DCId.Value
                   && doc1.AccessHash.Value == doc2.AccessHash.Value;
        }
    }

    public class TLDocumentEmpty : TLDocumentBase
    {
        public const uint Signature = TLConstructors.TLDocumentEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature), Id.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Id.ToStream(output);
        }
    }

    public abstract class TLDocument : TLDocumentBase
    {
        public TLLong AccessHash { get; set; }

        public TLInt Date { get; set; }

        public TLString MimeType { get; set; }

        public TLInt Size { get; set; }

        public override int DocumentSize
        {
            get { return Size != null ? Size.Value : 0; }
        }

        public TLPhotoSizeBase Thumb { get; set; }

        public TLInt DCId { get; set; }

        public byte[] Buffer { get; set; }

        public TLInputFileBase ThumbInputFile { get; set; }

        public TLInputDocumentFileLocation ToInputFileLocation()
        {
            return new TLInputDocumentFileLocation { AccessHash = AccessHash, Id = Id };
        }

        public abstract TLString FileName { get; set; }

        public string FileExt
        {
            get { return Path.GetExtension(FileName.ToString()).Replace(".", string.Empty); }
        }

        public string GetFileName()
        {
            return string.Format("document{0}_{1}.{2}", Id, AccessHash, FileExt);
        }
    }

    public class TLDocument10 : TLDocument
    {
        public const uint Signature = TLConstructors.TLDocument;

        public TLInt UserId { get; set; }

        public override TLString FileName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            FileName = GetObject<TLString>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            Thumb = GetObject<TLPhotoSizeBase>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                UserId.ToBytes(),
                Date.ToBytes(),
                FileName.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                Thumb.ToBytes(),
                DCId.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            FileName = GetObject<TLString>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            Thumb = GetObject<TLPhotoSizeBase>(input);
            DCId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            UserId.ToStream(output);
            Date.ToStream(output);
            FileName.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            Thumb.ToStream(output);
            DCId.ToStream(output);
        }
    }

    public interface IAttributes
    {
        TLVector<TLDocumentAttributeBase> Attributes { get; set; }
    }

    public class TLDocument22 : TLDocument, IAttributes
    {
        public const uint Signature = TLConstructors.TLDocument22;

        public TLVector<TLDocumentAttributeBase> Attributes { get; set; }

        public TLInt ImageSizeH
        {
            get
            {
                if (Attributes != null)
                {
                    for (var i = 0; i < Attributes.Count; i++)
                    {
                        var imageSizeAttribute = Attributes[i] as TLDocumentAttributeImageSize;
                        if (imageSizeAttribute != null)
                        {
                            return imageSizeAttribute.H;
                        }
                    }
                }

                return new TLInt(0);
            }
        }

        public TLInt ImageSizeW
        {
            get
            {
                if (Attributes != null)
                {
                    for (var i = 0; i < Attributes.Count; i++)
                    {
                        var imageSizeAttribute = Attributes[i] as TLDocumentAttributeImageSize;
                        if (imageSizeAttribute != null)
                        {
                            return imageSizeAttribute.W;
                        }
                    }
                }

                return new TLInt(0);
            }
        }

        public override TLString FileName
        {
            get
            {
                if (Attributes != null)
                {
                    for (var i = 0; i < Attributes.Count; i++)
                    {
                        var fileNameAttribute = Attributes[i] as TLDocumentAttributeFileName;
                        if (fileNameAttribute != null)
                        {
                            return fileNameAttribute.FileName;
                        }
                    }
                }

                return TLString.Empty;
            }
            set
            {
                Attributes = Attributes ?? new TLVector<TLDocumentAttributeBase>();

                for (var i = 0; i < Attributes.Count; i++)
                {
                    if (Attributes[i] is TLDocumentAttributeFileName)
                    {
                        Attributes.RemoveAt(i--);
                    }
                }

                Attributes.Add(new TLDocumentAttributeFileName{FileName = value});
            }
        }

        public TLInputStickerSetBase StickerSet
        {
            get
            {
                if (Attributes != null)
                {
                    for (var i = 0; i < Attributes.Count; i++)
                    {
                        var stickerAttribute = Attributes[i] as TLDocumentAttributeSticker29;
                        if (stickerAttribute != null)
                        {
                            return stickerAttribute.Stickerset;
                        }
                    }
                }

                return null;
            }
        }

        #region Additional
        public string Emoticon { get; set; }
        #endregion

        public override string ToString()
        {
            return string.Format("{0} Id={1}", GetType().Name, Id) + (StickerSet != null ? StickerSet.ToString() : string.Empty);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            Thumb = GetObject<TLPhotoSizeBase>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);
            Attributes = GetObject<TLVector<TLDocumentAttributeBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                Date.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                Thumb.ToBytes(),
                DCId.ToBytes(),
                Attributes.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            Date = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            Thumb = GetObject<TLPhotoSizeBase>(input);
            DCId = GetObject<TLInt>(input);
            Attributes = GetObject<TLVector<TLDocumentAttributeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            Date.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            Thumb.ToStream(output);
            DCId.ToStream(output);
            Attributes.ToStream(output);
        }
    }
}
