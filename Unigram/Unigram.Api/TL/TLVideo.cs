using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLVideoBase : TLObject
    {
        public TLLong Id { get; set; }

        public virtual int VideoSize { get { return 0; } }
    }

    public class TLVideoEmpty : TLVideoBase
    {
        public const uint Signature = TLConstructors.TLVideoEmpty;

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

    public class TLVideo33 : TLVideo28
    {
        public new const uint Signature = TLConstructors.TLVideo33;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            //UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Duration = GetObject<TLInt>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            Thumb = GetObject<TLPhotoSizeBase>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                //UserId.ToBytes(),
                Date.ToBytes(),
                Duration.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                Thumb.ToBytes(),
                DCId.ToBytes(),
                W.ToBytes(),
                H.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            //UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Duration = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            Thumb = GetObject<TLPhotoSizeBase>(input);
            DCId = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            //UserId.ToStream(output);
            Date.ToStream(output);
            Duration.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            Thumb.ToStream(output);
            DCId.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
        }
    }

    public class TLVideo28 : TLVideo
    {
        public new const uint Signature = TLConstructors.TLVideo28;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Duration = GetObject<TLInt>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            Thumb = GetObject<TLPhotoSizeBase>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);

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
                Duration.ToBytes(),
                Size.ToBytes(),
                Thumb.ToBytes(),
                DCId.ToBytes(),
                W.ToBytes(),
                H.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Duration = GetObject<TLInt>(input);
            Size = GetObject<TLInt>(input);
            Thumb = GetObject<TLPhotoSizeBase>(input);
            DCId = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            UserId.ToStream(output);
            Date.ToStream(output);
            Duration.ToStream(output);
            Size.ToStream(output);
            Thumb.ToStream(output);
            DCId.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
        }
    }

    public class TLVideo : TLVideoBase
    {
        public const uint Signature = TLConstructors.TLVideo;

        public TLLong AccessHash { get; set; }
        public TLInt UserId { get; set; }
        public TLInt Date { get; set; }
        public TLString Caption { get; set; }
        public TLInt Duration { get; set; }
        public TLString MimeType { get; set; }
        public TLInt Size { get; set; }
        public TLPhotoSizeBase Thumb { get; set; }
        public TLInt DCId { get; set; }
        public TLInt W { get; set; }
        public TLInt H { get; set; }

        public string GetFileName()
        {
            return string.Format("video{0}_{1}.mp4", Id, AccessHash);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Caption = GetObject<TLString>(bytes, ref position);
            Duration = GetObject<TLInt>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            Thumb = GetObject<TLPhotoSizeBase>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);

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
                Caption.ToBytes(),
                Duration.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                Thumb.ToBytes(),
                DCId.ToBytes(),
                W.ToBytes(),
                H.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Caption = GetObject<TLString>(input);
            Duration = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            Thumb = GetObject<TLPhotoSizeBase>(input);
            DCId = GetObject<TLInt>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            UserId.ToStream(output);
            Date.ToStream(output);
            Caption.ToStream(output);
            Duration.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            Thumb.ToStream(output);
            DCId.ToStream(output);
            W.ToStream(output);
            H.ToStream(output);
        }

        #region Additional

        public override int VideoSize
        {
            get { return Size != null ? Size.Value : 0; }
        }

        public string DurationString
        {
            get
            {
                var timeSpan = TimeSpan.FromSeconds(Duration.Value);

                if (timeSpan.Hours > 0)
                {
                    return timeSpan.ToString(@"h\:mm\:ss");
                }

                return timeSpan.ToString(@"m\:ss");
            }
        }

        public TLInputFile ThumbInputFile { get; set; }

        #endregion

        public TLInputVideoFileLocation ToInputFileLocation()
        {
            return new TLInputVideoFileLocation {AccessHash = AccessHash, Id = Id};
        }
    }
}
