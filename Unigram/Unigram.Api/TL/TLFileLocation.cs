using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLFileLocationBase : TLObject//, IFileData
    {
        public TLLong VolumeId { get; set; }

        public TLInt LocalId { get; set; }

        public TLLong Secret { get; set; }

        #region Additional

        //public string SendingFileName { get; set; }
        //public byte[] Buffer { get; set; }
        //public byte[] Bytes { get; set; }
        #endregion

        public virtual void Update(TLFileLocationBase fileLocation)
        {
            if (fileLocation != null)
            {
                //if (Buffer == null || LocalId.Value != fileLocation.LocalId.Value)
                //{
                //    Buffer = fileLocation.Buffer;
                //}

                VolumeId = fileLocation.VolumeId;
                LocalId = fileLocation.LocalId;
                Secret = fileLocation.Secret;

            }
        }
    }

    public class TLFileLocationUnavailable : TLFileLocationBase
    {
        public const uint Signature = TLConstructors.TLFileLocationUnavailable;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            VolumeId = GetObject<TLLong>(bytes, ref position);
            LocalId = GetObject<TLInt>(bytes, ref position);
            Secret = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                VolumeId.ToBytes(),
                LocalId.ToBytes(),
                Secret.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            VolumeId = GetObject<TLLong>(input);
            LocalId = GetObject<TLInt>(input);
            Secret = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(VolumeId.ToBytes());
            output.Write(LocalId.ToBytes());
            output.Write(Secret.ToBytes());
        }
    }

    public class TLFileLocation : TLFileLocationBase
    {
        public const uint Signature = TLConstructors.TLFileLocation;
        
        public TLInt DCId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            DCId = GetObject<TLInt>(bytes, ref position);
            VolumeId = GetObject<TLLong>(bytes, ref position);
            LocalId = GetObject<TLInt>(bytes, ref position);
            Secret = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                DCId.ToBytes(),
                VolumeId.ToBytes(),
                LocalId.ToBytes(),
                Secret.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            DCId = GetObject<TLInt>(input);
            VolumeId = GetObject<TLLong>(input);
            LocalId = GetObject<TLInt>(input);
            Secret = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(DCId.ToBytes());
            output.Write(VolumeId.ToBytes());
            output.Write(LocalId.ToBytes());
            output.Write(Secret.ToBytes());
        }

        public override string ToString()
        {
            return string.Format("File: DCId {0}, VolumeId {1}, LocalId {2}, Secret {3}", DCId, LocalId.Value, VolumeId.Value, Secret.Value);
        }

        public override void Update(TLFileLocationBase baseFileLocation)
        {
            base.Update(baseFileLocation);

            var fileLocation = baseFileLocation as TLFileLocation;
            if (fileLocation != null)
            {
                DCId = fileLocation.DCId;
            }
        }

        public TLInputFileLocation ToInputFileLocation()
        {
            return new TLInputFileLocation
            {
                LocalId = LocalId,
                Secret = Secret,
                VolumeId = VolumeId
            };
        }
    }
}