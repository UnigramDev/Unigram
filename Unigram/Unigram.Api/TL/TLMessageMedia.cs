using System.IO;
#if WP81
using Windows.Media.Transcoding;
#endif
#if WP8
using Windows.Storage;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Services.Cache;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLMessageMediaBase : TLObject
    {
        public TLMessageMediaBase Self { get { return this; } }

        public virtual int MediaSize { get { return 0; } }

        private double _uploadingProgress;

        public double UploadingProgress
        {
            get { return _uploadingProgress; }
            set { SetField(ref _uploadingProgress, value, () => UploadingProgress); }
        }

        private double _downloadingProgress;

        public double DownloadingProgress
        {
            get { return _downloadingProgress; }
            set { SetField(ref _downloadingProgress, value, () => DownloadingProgress); }
        }

        public double LastProgress { get; set; }

        public bool NotListened { get; set; }

        /// <summary>
        /// For internal use
        /// </summary>
        public TLLong FileId { get; set; }

        public string IsoFileName { get; set; }

#if WP8
        public StorageFile File { get; set; }
#endif
#if WP81
        public PrepareTranscodeResult TranscodeResult { get; set; }
#endif

        public bool IsCanceled { get; set; }
    }

    public class TLMessageMediaEmpty : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
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

    public class TLMessageMediaDocument : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaDocument;

        public TLDocumentBase Document { get; set; }

        public override int MediaSize
        {
            get
            {
                return Document.DocumentSize;
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Document = GetObject<TLDocumentBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Document = GetObject<TLDocumentBase>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Document.ToStream(output);
            
            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }
    }

    public class TLMessageMediaAudio : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaAudio;

        public TLAudioBase Audio { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Audio = GetObject<TLAudioBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Audio = GetObject<TLAudioBase>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Audio.ToStream(output);

            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }
    }

    public class TLMessageMediaPhoto28 : TLMessageMediaPhoto
    {
        public new const uint Signature = TLConstructors.TLMessageMediaPhoto28;

        public TLString Caption { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Photo = GetObject<TLPhotoBase>(bytes, ref position);
            Caption = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Photo = GetObject<TLPhotoBase>(input);
            Caption = GetObject<TLString>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Photo.ToStream(output);
            Caption.ToStream(output);

            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }

        public override string ToString()
        {
            return Photo.ToString();
        }
    }

    public class TLMessageMediaPhoto : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaPhoto;

        public TLPhotoBase Photo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Photo = GetObject<TLPhotoBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Photo = GetObject<TLPhotoBase>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Photo.ToStream(output);

            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }

        public override string ToString()
        {
            return Photo.ToString();
        }
    }

    public class TLMessageMediaVideo28 : TLMessageMediaVideo
    {
        public new const uint Signature = TLConstructors.TLMessageMediaVideo28;

        public TLString Caption { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Video = GetObject<TLVideoBase>(bytes, ref position);
            Caption = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Video = GetObject<TLVideoBase>(input);
            Caption = GetObject<TLString>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Video.ToStream(output);
            Caption.ToStream(output);

            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }
    }

    public class TLMessageMediaVideo : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaVideo;

        public TLVideoBase Video { get; set; }

        public override int MediaSize
        {
            get
            {
                return Video.VideoSize;
            }
        }


        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Video = GetObject<TLVideoBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Video = GetObject<TLVideoBase>(input);

            var isoFileName = GetObject<TLString>(input);
            IsoFileName = isoFileName.ToString();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Video.ToStream(output);

            var isoFileName = new TLString(IsoFileName);
            isoFileName.ToStream(output);
        }
    }

    public class TLMessageMediaGeo : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaGeo;

        public TLGeoPointBase Geo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Geo = GetObject<TLGeoPointBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Geo = GetObject<TLGeoPointBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Geo.ToStream(output);
        }
    }

    public class TLMessageMediaVenue : TLMessageMediaGeo
    {
        public new const uint Signature = TLConstructors.TLMessageMediaVenue;

        public TLString Title { get; set; }

        public TLString Address { get; set; }

        public TLString Provider { get; set; }

        public TLString VenueId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Geo = GetObject<TLGeoPointBase>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            Address = GetObject<TLString>(bytes, ref position);
            Provider = GetObject<TLString>(bytes, ref position);
            VenueId = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Geo = GetObject<TLGeoPointBase>(input);
            Title = GetObject<TLString>(input);
            Address = GetObject<TLString>(input);
            Provider = GetObject<TLString>(input);
            VenueId = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Geo.ToStream(output);
            Title.ToStream(output);
            Address.ToStream(output);
            Provider.ToStream(output);
            VenueId.ToStream(output);
        }
    }

    public class TLMessageMediaContact : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaContact;

        public TLString PhoneNumber { get; set; }

        public TLString FirstName { get; set; }

        public TLString LastName { get; set; }

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneNumber = GetObject<TLString>(bytes, ref position);
            FirstName = GetObject<TLString>(bytes, ref position);
            LastName = GetObject<TLString>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            PhoneNumber = GetObject<TLString>(input);
            FirstName = GetObject<TLString>(input);
            LastName = GetObject<TLString>(input);
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            PhoneNumber.ToStream(output);
            FirstName.ToStream(output);
            LastName.ToStream(output);
            UserId.ToStream(output);
        }

        public override string ToString()
        {
            return FullName;
        }
        #region Additional

        public virtual string FullName { get { return string.Format("{0} {1}", FirstName, LastName); } }

        public TLUserBase User
        {
            get
            {
                var cacheService = InMemoryCacheService.Instance;
                return cacheService.GetUser(UserId);
            }
        }
        #endregion
    }

    public abstract class TLMessageMediaUnsupportedBase : TLMessageMediaBase { }

    public class TLMessageMediaUnsupported : TLMessageMediaUnsupportedBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaUnsupported;

        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Bytes = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Bytes = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Bytes.ToStream(output);
        }
    }

    public class TLMessageMediaUnsupported24 : TLMessageMediaUnsupportedBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaUnsupported24;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
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

    public class TLMessageMediaWebPage : TLMessageMediaBase
    {
        public const uint Signature = TLConstructors.TLMessageMediaWebPage;

        public TLWebPageBase WebPage { get; set; }

        #region Additional
        public TLMessageMediaBase Self { get { return this; } }

        public TLPhotoBase Photo
        {
            get
            {
                var webPage = WebPage as TLWebPage;
                if (webPage != null)
                {
                    return webPage.Photo;
                }

                return null;
            }
        }

        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            WebPage = GetObject<TLWebPageBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            WebPage = GetObject<TLWebPageBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            WebPage.ToStream(output);
        }
    }
}
