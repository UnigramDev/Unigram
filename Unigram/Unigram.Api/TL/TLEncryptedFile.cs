using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLEncryptedFileBase : TLObject { }

    public class TLEncryptedFileEmpty : TLEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLEncryptedFileEmpty;

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

    public class TLEncryptedFile : TLEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLEncryptedFile;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public TLInt Size { get; set; }

        public TLInt DCId { get; set; }

        public TLInt KeyFingerprint { get; set; }

        #region Additional
        public TLString FileName { get; set; }

        public string FileExt
        {
            get
            {
                return FileName != null
                    ? Path.GetExtension(FileName.ToString()).Replace(".", string.Empty)
                    : string.Empty;
            }
        }

        public TLInt Duration { get; set; }

        public string DurationString
        {
            get
            {
                if (Duration == null) return string.Empty;

                var timeSpan = TimeSpan.FromSeconds(Duration.Value);

                if (timeSpan.Hours > 0)
                {
                    return timeSpan.ToString(@"h\:mm\:ss");
                }

                return timeSpan.ToString(@"m\:ss");
            }
        }
        #endregion 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);
            KeyFingerprint = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            Size = GetObject<TLInt>(input);
            DCId = GetObject<TLInt>(input);
            KeyFingerprint = GetObject<TLInt>(input);

            FileName = GetNullableObject<TLString>(input);
            Duration = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Size.ToBytes());
            output.Write(DCId.ToBytes());
            output.Write(KeyFingerprint.ToBytes());

            FileName.NullableToStream(output);
            Duration.NullableToStream(output);
        }
    }
}
