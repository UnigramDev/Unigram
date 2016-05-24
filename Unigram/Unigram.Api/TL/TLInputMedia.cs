using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputMediaBase : TLObject
    {
#region Additional
        public byte[] MD5Hash { get; set; }
#endregion
    }

    public class TLInputMediaEmpty : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaEmpty;

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

    public class TLInputMediaUploadedDocument : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedDocument;

        public TLInputFileBase File { get; set; }

        public TLString FileName { get; set; }

        public TLString MimeType { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                FileName.ToBytes(),
                MimeType.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            FileName = GetObject<TLString>(input);
            MimeType = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            FileName.ToStream(output);
            MimeType.ToStream(output);
        }
    }

    public class TLInputMediaUploadedDocument22 : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedDocument22;

        public TLInputFileBase File { get; set; }

        public TLString MimeType { get; set; }

        public TLVector<TLDocumentAttributeBase> Attributes { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                MimeType.ToBytes(),
                Attributes.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            MimeType = GetObject<TLString>(input);
            Attributes = GetObject<TLVector<TLDocumentAttributeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            MimeType.ToStream(output);
            Attributes.ToStream(output);
        }
    }

    public class TLInputMediaUploadedThumbDocument : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedThumbDocument;

        public TLInputFileBase File { get; set; }

        public TLInputFileBase Thumb { get; set; }

        public TLString FileName { get; set; }

        public TLString MimeType { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Thumb.ToBytes(),
                FileName.ToBytes(),
                MimeType.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFileBase>(input);
            Thumb = GetObject<TLInputFileBase>(input);
            FileName = GetObject<TLString>(input);
            MimeType = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Thumb.ToStream(output);
            FileName.ToStream(output);
            MimeType.ToStream(output);
        }
    }

    public class TLInputMediaUploadedThumbDocument22 : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedThumbDocument22;

        public TLInputFileBase File { get; set; }

        public TLInputFileBase Thumb { get; set; }

        public TLString MimeType { get; set; }

        public TLVector<TLDocumentAttributeBase> Attributes { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Thumb.ToBytes(),
                MimeType.ToBytes(),
                Attributes.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFileBase>(input);
            Thumb = GetObject<TLInputFileBase>(input);
            MimeType = GetObject<TLString>(input);
            Attributes = GetObject<TLVector<TLDocumentAttributeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Thumb.ToStream(output);
            MimeType.ToStream(output);
            Attributes.ToStream(output);
        }
    }

    public class TLInputMediaDocument : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaDocument;

        public TLInputDocumentBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInputDocumentBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
        }
    }

    public class TLInputMediaUploadedAudio : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedAudio;

        public TLInputFile File { get; set; }

        public TLInt Duration { get; set; }

        public TLString MimeType { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Duration.ToBytes(),
                MimeType.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Duration.ToStream(output);
            MimeType.ToStream(output);
        }
    }

    public class TLInputMediaAudio : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaAudio;

        public TLInputAudioBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }

    public class TLInputMediaUploadedPhoto : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedPhoto;

        public TLInputFileBase File { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
        }
    }

    public class TLInputMediaUploadedPhoto28 : TLInputMediaUploadedPhoto, IInputMediaCaption
    {
        public new const uint Signature = TLConstructors.TLInputMediaUploadedPhoto28;

        public TLString Caption { get; set; }
   
        public TLInputMediaUploadedPhoto28()
        {
            
        }

        public TLInputMediaUploadedPhoto28(TLInputMediaUploadedPhoto inputMediaUploadedPhoto, TLString caption)
        {
            File = inputMediaUploadedPhoto.File;
            Caption = caption;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaPhoto : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaPhoto;

        public TLInputPhotoBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInputPhotoBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
        }
    }

    public class TLInputMediaPhoto28 : TLInputMediaPhoto, IInputMediaCaption
    {
        public new const uint Signature = TLConstructors.TLInputMediaPhoto28;

        public TLString Caption { get; set; }

        public TLInputMediaPhoto28()
        {
            
        }

        public TLInputMediaPhoto28(TLInputMediaPhoto inputMediaPhoto, TLString caption)
        {
            Id = inputMediaPhoto.Id;
            Caption = caption;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInputPhotoBase>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaGeoPoint : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaGeoPoint;

        public TLInputGeoPointBase GeoPoint { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                GeoPoint.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            GeoPoint = GetObject<TLInputGeoPointBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            GeoPoint.ToStream(output);
        }
    }

    public class TLInputMediaVenue : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaVenue;

        public TLInputGeoPointBase GeoPoint { get; set; }

        public TLString Title { get; set; }

        public TLString Address { get; set; }

        public TLString Provider { get; set; }

        public TLString VenueId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                GeoPoint.ToBytes(),
                Title.ToBytes(),
                Address.ToBytes(),
                Provider.ToBytes(),
                VenueId.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            GeoPoint = GetObject<TLInputGeoPointBase>(input);
            Title = GetObject<TLString>(input);
            Address = GetObject<TLString>(input);
            Provider = GetObject<TLString>(input);
            VenueId = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            GeoPoint.ToStream(output);
            Title.ToStream(output);
            Address.ToStream(output);
            Provider.ToStream(output);
            VenueId.ToStream(output);
        }
    }

    public class TLInputMediaContact : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaContact;

        public TLString PhoneNumber { get; set; }
        public TLString FirstName { get; set; }
        public TLString LastName { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            PhoneNumber = GetObject<TLString>(input);
            FirstName = GetObject<TLString>(input);
            LastName = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            PhoneNumber.ToStream(output);
            FirstName.ToStream(output);
            LastName.ToStream(output);
        }
    }

    public class TLInputMediaUploadedVideo : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedVideo;

        public TLInputFileBase File { get; set; }
        public TLInt Duration { get; set; }
        public TLInt W { get; set; }
        public TLInt H { get; set; }
        public TLString MimeType { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                MimeType.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            MimeType.ToStream(output);
        }
    }

    public class TLInputMediaUploadedVideo28 : TLInputMediaUploadedVideo, IInputMediaCaption
    {
        public new const uint Signature = TLConstructors.TLInputMediaUploadedVideo28;

        public TLString Caption { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaUploadedVideo36 : TLInputMediaUploadedVideo28
    {
        public new const uint Signature = TLConstructors.TLInputMediaUploadedVideo36;

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                MimeType.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            MimeType.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaUploadedThumbVideo : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaUploadedThumbVideo;

        public TLInputFileBase File { get; set; }
        public TLInputFile Thumb { get; set; }
        public TLInt Duration { get; set; }
        public TLInt W { get; set; }
        public TLInt H { get; set; }
        public TLString MimeType { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Thumb.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                MimeType.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Thumb = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Thumb.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            MimeType.ToStream(output);
        }
    }

    public class TLInputMediaUploadedThumbVideo28 : TLInputMediaUploadedThumbVideo, IInputMediaCaption
    {
        public new const uint Signature = TLConstructors.TLInputMediaUploadedThumbVideo28;

        public TLString Caption { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Thumb.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Thumb = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Thumb.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaUploadedThumbVideo36 : TLInputMediaUploadedThumbVideo28
    {
        public new const uint Signature = TLConstructors.TLInputMediaUploadedThumbVideo36;

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                File.ToBytes(),
                Thumb.ToBytes(),
                Duration.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                MimeType.ToBytes(),
                Caption.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            File = GetObject<TLInputFile>(input);
            Thumb = GetObject<TLInputFile>(input);
            Duration = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Caption = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            File.ToStream(output);
            Thumb.ToStream(output);
            Duration.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
            MimeType.ToStream(output);
            Caption.ToStream(output);
        }
    }

    public class TLInputMediaVideo : TLInputMediaBase
    {
        public const uint Signature = TLConstructors.TLInputMediaVideo;

        public TLInputVideoBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }

    public class TLInputMediaVideo28 : TLInputMediaVideo, IInputMediaCaption
    {
        public new const uint Signature = TLConstructors.TLInputMediaVideo28;

        public TLString Caption { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Caption.ToBytes());
        }
    }

    public interface IInputMediaCaption
    {
        TLString Caption { get; set; }
    }
}
