namespace Telegram.Api.TL
{
    public class TLFileTypeBase : TLObject { }

    public class TLFileTypeUnknown : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeUnknown;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeUnknown--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeJpeg : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeJpeg;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeJpeg--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeGif : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeGif;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeGif--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypePng : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypePng;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypePng--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeMp3 : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeMp3;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeMp3--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeMov : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeMov;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeMov--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypePartial : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypePartial;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypePartial--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeMp4 : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeMp4;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeMp4--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLFileTypeWebp : TLFileTypeBase
    {
        public const uint Signature = TLConstructors.TLFileTypeWebp;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFileTypeWebp--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }
}