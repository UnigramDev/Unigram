using System;
using System.IO;
using System.Linq;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLPhotoBase : TLObject
    {
        public abstract void Update(TLPhotoBase photo);

        public TLPhotoBase Self{ get { return this; } }
    }

    public abstract class TLPhotoCommon : TLPhotoBase
    {
        public TLFileLocationBase PhotoSmall { get; set; }

        public TLFileLocationBase PhotoBig { get; set; }

        public override void Update(TLPhotoBase photo)
        {
            var photoCommon = photo as TLPhotoCommon;
            if (photoCommon != null)
            {
                if (PhotoSmall != null)
                {
                    PhotoSmall.Update(photoCommon.PhotoSmall);
                }
                else
                {
                    PhotoSmall = photoCommon.PhotoSmall;
                }
                if (PhotoBig != null)
                {
                    PhotoBig.Update(photoCommon.PhotoBig);
                }
                else
                {
                    PhotoBig = photoCommon.PhotoBig;
                }
            }
        }
    }

    public abstract class TLMediaPhotoBase : TLPhotoBase
    {
        public TLLong Id { get; set; }

        public override void Update(TLPhotoBase photo)
        {
            var mediaPhoto = photo as TLMediaPhotoBase;
            if (mediaPhoto != null)
            {
                Id = mediaPhoto.Id;
            }
        }
    }

    public class TLPhotoEmpty : TLMediaPhotoBase
    {
        public const uint Signature = TLConstructors.TLPhotoEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }
    }

    public class TLPhoto33 : TLPhoto28
    {
        public new const uint Signature = TLConstructors.TLPhoto33;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            Date = GetObject<TLInt>(input);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Date.ToBytes());
            Sizes.ToStream(output);
        }
    }

    public class TLPhoto28 : TLPhoto
    {
        public new const uint Signature = TLConstructors.TLPhoto28;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Geo = GetObject<TLGeoPointBase>(bytes, ref position);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Geo = GetObject<TLGeoPointBase>(input);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(UserId.ToBytes());
            output.Write(Date.ToBytes());
            Geo.ToStream(output);
            Sizes.ToStream(output);
        }

        public override void Update(TLPhotoBase photo)
        {
            var p = photo as TLPhoto28;
            if (p != null)
            {
                base.Update(p);

                AccessHash = p.AccessHash;
                UserId = p.UserId;
                Date = p.Date;
                Caption = p.Caption;
                Geo = p.Geo;

                if (AccessHash.Value != p.AccessHash.Value)
                {
                    Sizes = p.Sizes;
                }
                else
                {
                    for (var i = 0; i < Sizes.Count; i++)
                    {
                        Sizes[i].Update(p.Sizes[i]);
                    }
                }
            }
        }
    }

    public class TLPhoto : TLMediaPhotoBase
    {
        public const uint Signature = TLConstructors.TLPhoto;

        public TLLong AccessHash { get; set; }

        public TLInt UserId { get; set; }

        public TLInt Date { get; set; }

        public TLString Caption { get; set; }

        public TLGeoPointBase Geo { get; set; }

        public TLVector<TLPhotoSizeBase> Sizes { get; set; }

        public string GetFileName()
        {
            var photoSize = Sizes.FirstOrDefault() as TLPhotoSize;
            if (photoSize != null)
            {
                var fileLocation = photoSize.Location;
                if (fileLocation == null) return null;

                var fileName = String.Format("{0}_{1}_{2}.jpg",
                    fileLocation.VolumeId,
                    fileLocation.LocalId,
                    fileLocation.Secret);

                return fileName;
            }

            return null;
        }
        
        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Caption = GetObject<TLString>(bytes, ref position);
            Geo = GetObject<TLGeoPointBase>(bytes, ref position);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Caption = GetObject<TLString>(input);
            Geo = GetObject<TLGeoPointBase>(input);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(UserId.ToBytes());
            output.Write(Date.ToBytes());
            output.Write(Caption.ToBytes());
            Geo.ToStream(output);
            Sizes.ToStream(output);
        }

        public override void Update(TLPhotoBase photo)
        {
            var p = photo as TLPhoto;
            if (p != null)
            {
                base.Update(p);

                AccessHash = p.AccessHash;
                UserId = p.UserId;
                Date = p.Date;
                Caption = p.Caption;
                Geo = p.Geo;

                if (AccessHash.Value != p.AccessHash.Value)
                {
                    Sizes = p.Sizes;
                }
                else
                {
                    for (var i = 0; i < Sizes.Count; i++)
                    {
                        Sizes[i].Update(p.Sizes[i]);
                    }
                }
            }
        }

        public TLPhotoCachedSize CachedSize
        {
            get
            {
                return Sizes != null ? (TLPhotoCachedSize)Sizes.FirstOrDefault(x => x is TLPhotoCachedSize) : null;
            }
        }

        public override string ToString()
        {
            return string.Format("TLPhoto Sizes=[{0}]", string.Join(", ", Sizes.Select(x => x.ToString())));
        }
    }

    public class TLUserProfilePhotoEmpty : TLPhotoBase
    {
        public const uint Signature = TLConstructors.TLUserProfilePhotoEmpty;

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

        public override void Update(TLPhotoBase photo)
        {

        }
    }

    public class TLUserProfilePhoto : TLPhotoCommon
    {
        public const uint Signature = TLConstructors.TLUserProfilePhoto;

        public TLLong PhotoId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhotoId = GetObject<TLLong>(bytes, ref position);
            PhotoSmall = GetObject<TLFileLocationBase>(bytes, ref position);
            PhotoBig = GetObject<TLFileLocationBase>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            PhotoId = GetObject<TLLong>(input);
            PhotoSmall = GetObject<TLFileLocationBase>(input);
            PhotoBig = GetObject<TLFileLocationBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(PhotoId != null ? PhotoId.ToBytes() : new TLLong(0).ToBytes());
            PhotoSmall.ToStream(output);
            PhotoBig.ToStream(output);
        }
    }

    public class TLChatPhotoEmpty : TLPhotoBase
    {
        public const uint Signature = TLConstructors.TLChatPhotoEmpty;

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

        public override void Update(TLPhotoBase photo)
        {

        }
    }

    public class TLChatPhoto : TLPhotoCommon
    {
        public const uint Signature = TLConstructors.TLChatPhoto;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhotoSmall = GetObject<TLFileLocationBase>(bytes, ref position);
            PhotoBig = GetObject<TLFileLocationBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            PhotoSmall = GetObject<TLFileLocationBase>(input);
            PhotoBig = GetObject<TLFileLocationBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            PhotoSmall.ToStream(output);
            PhotoBig.ToStream(output);
        }
    }
}