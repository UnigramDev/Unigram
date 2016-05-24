using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputEncryptedFileBase : TLObject { }

    public class TLInputEncryptedFileEmpty : TLInputEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLInputEncryptedFileEmpty;

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

    public class TLInputEncryptedFileUploaded : TLInputEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLInputEncryptedFileUploaded;

        public TLLong Id { get; set; }

        public TLInt Parts { get; set; }

        public TLString MD5Checksum { get; set; }

        public TLInt KeyFingerprint { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Parts = GetObject<TLInt>(bytes, ref position);
            MD5Checksum = GetObject<TLString>(bytes, ref position);
            KeyFingerprint = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Parts.ToBytes(),
                MD5Checksum.ToBytes(),
                KeyFingerprint.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            Parts = GetObject<TLInt>(input);
            MD5Checksum = GetObject<TLString>(input);
            KeyFingerprint = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Parts.ToBytes());
            output.Write(MD5Checksum.ToBytes());
            output.Write(KeyFingerprint.ToBytes());
        }
    }

    public class TLInputEncryptedFileBigUploaded : TLInputEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLInputEncryptedFileBigUploaded;

        public TLLong Id { get; set; }

        public TLInt Parts { get; set; }

        public TLInt KeyFingerprint { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Parts = GetObject<TLInt>(bytes, ref position);
            KeyFingerprint = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Parts.ToBytes(),
                KeyFingerprint.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            Parts = GetObject<TLInt>(input);
            KeyFingerprint = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Parts.ToBytes());
            output.Write(KeyFingerprint.ToBytes());
        }
    }

    public class TLInputEncryptedFile : TLInputEncryptedFileBase
    {
        public const uint Signature = TLConstructors.TLInputEncryptedFile;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }


        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
        }
    }
}
