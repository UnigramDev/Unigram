using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputFileBase : TLObject { }

    public class TLInputFile : TLInputFileBase
    {
        public const uint Signature = TLConstructors.TLInputFile;

        public TLLong Id { get; set; }
        public TLInt Parts { get; set; }
        public TLString Name { get; set; }
        public TLString MD5Checksum { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Parts = GetObject<TLInt>(bytes, ref position);
            Name = GetObject<TLString>(bytes, ref position);
            MD5Checksum = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Parts.ToBytes(),
                Name.ToBytes(),
                MD5Checksum.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            Parts = GetObject<TLInt>(input);
            Name = GetObject<TLString>(input);
            MD5Checksum = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            Parts.ToStream(output);
            Name.ToStream(output);
            MD5Checksum.ToStream(output);
        }
    }

    public class TLInputFileBig : TLInputFileBase
    {
        public const uint Signature = TLConstructors.TLInputFileBig;

        public TLLong Id { get; set; }

        public TLInt Parts { get; set; }

        public TLString Name { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Parts = GetObject<TLInt>(bytes, ref position);
            Name = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Parts.ToBytes(),
                Name.ToBytes());
        }
    }
}
