using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [KnownType(typeof(TLPhotoSizeEmpty))]
    [KnownType(typeof(TLPhotoSize))]
    [KnownType(typeof(TLPhotoCachedSize))]
    [DataContract]
    public abstract class TLPhotoSizeBase : TLObject
    {
        [DataMember]
        public TLString Type { get; set; }

        public virtual void Update(TLPhotoSizeBase photoSizeBase)
        {
            if (photoSizeBase != null)
            {
                Type = photoSizeBase.Type;
            }
        }
    }

    [DataContract]
    public class TLPhotoSizeEmpty : TLPhotoSizeBase
    {
        public const uint Signature = TLConstructors.TLPhotoSizeEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Type = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Type.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Type = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Type.ToBytes());
        }

        public override string ToString()
        {
            return string.Format("empty {0}", Type);
        }
    }

    [DataContract]
    public class TLPhotoSize : TLPhotoSizeBase
    {
        public const uint Signature = TLConstructors.TLPhotoSize;

        [DataMember]
        public TLFileLocationBase Location { get; set; }

        [DataMember]
        public TLInt W { get; set; }

        [DataMember]
        public TLInt H { get; set; }

        [DataMember]
        public TLInt Size { get; set; }

        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Type = GetObject<TLString>(bytes, ref position);
            Location = GetObject<TLFileLocationBase>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Type.ToBytes(),
                Location.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                Size.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Type = GetObject<TLString>(input);
            Location = GetObject<TLFileLocationBase>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            Size = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Type.ToBytes());
            Location.ToStream(output);
            output.Write(W.ToBytes());
            output.Write(H.ToBytes());
            output.Write(Size.ToBytes());
        }

        public override void Update(TLPhotoSizeBase photoSizeBase)
        {
            base.Update(photoSizeBase);

            var photoSize = photoSizeBase as TLPhotoSize;
            if (photoSize != null)
            {
                W = photoSize.W;
                H = photoSize.H;
                Size = photoSize.Size;
                if (Location != null)
                {
                    Location.Update(photoSize.Location);
                }
                else
                {
                    Location = photoSize.Location;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}{1}x{2} {3}", Type, H, W, Size);
        }
    }

    [DataContract]
    public class TLPhotoCachedSize : TLPhotoSizeBase
    {
        public const uint Signature = TLConstructors.TLPhotoCachedSize;

        [DataMember]
        public TLFileLocationBase Location { get; set; }

        [DataMember]
        public TLInt W { get; set; }

        [DataMember]
        public TLInt H { get; set; }

        [DataMember]
        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Type = GetObject<TLString>(bytes, ref position);
            Location = GetObject<TLFileLocationBase>(bytes, ref position);
            W = GetObject<TLInt>(bytes, ref position);
            H = GetObject<TLInt>(bytes, ref position);
            Bytes = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Type.ToBytes(),
                Location.ToBytes(),
                W.ToBytes(),
                H.ToBytes(),
                Bytes.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Type = GetObject<TLString>(input);
            Location = GetObject<TLFileLocationBase>(input);
            W = GetObject<TLInt>(input);
            H = GetObject<TLInt>(input);
            Bytes = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Type.ToBytes());
            Location.ToStream(output);
            output.Write(W.ToBytes());
            output.Write(H.ToBytes());
            output.Write(Bytes.ToBytes());
        }

        public override void Update(TLPhotoSizeBase photoSizeBase)
        {
            base.Update(photoSizeBase);

            var photoSize = photoSizeBase as TLPhotoCachedSize;
            if (photoSize != null)
            {
                W = photoSize.W;
                H = photoSize.H;
                Bytes = photoSize.Bytes;
                if (Location != null)
                {
                    Location.Update(photoSize.Location);
                }
                else
                {
                    Location = photoSize.Location;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("cached {0}{1}x{2}", Type, H, W);
        }
    }
}
