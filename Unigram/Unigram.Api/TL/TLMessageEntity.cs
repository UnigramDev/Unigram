using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLMessageEntityBase : TLObject
    {
        public TLInt Offset { get; set; }

        public TLInt Length { get; set; }
    }

    public class TLMessageEntityUnknown : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityUnknown;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityMention : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityMention;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityHashtag : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityHashtag;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityBotCommand : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityBotCommand;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityUrl : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityUrl;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityEmail : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityEmail;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityBold : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityBold;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityItalic : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityItalic;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityCode : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityCode;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLMessageEntityPre : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityPre;

        public TLString Language { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);
            Language = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes(),
                Language.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
            Language.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);
            Language = GetObject<TLString>(input);

            return this;
        }
    }

    public class TLMessageEntityTextUrl : TLMessageEntityBase
    {
        public const uint Signature = TLConstructors.TLMessageEntityTextUrl;

        public TLString Url { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Offset = GetObject<TLInt>(bytes, ref position);
            Length = GetObject<TLInt>(bytes, ref position);
            Url = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Length.ToBytes(),
                Url.ToBytes());
        }


        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Offset.ToStream(output);
            Length.ToStream(output);
            Url.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Offset = GetObject<TLInt>(input);
            Length = GetObject<TLInt>(input);
            Url = GetObject<TLString>(input);

            return this;
        }
    }
}
