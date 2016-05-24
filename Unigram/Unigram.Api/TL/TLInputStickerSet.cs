using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputStickerSetBase : TLObject
    {
        public abstract string Name { get; }
    }

    public class TLInputStickerSetEmpty : TLInputStickerSetBase
    {
        public const uint Signature = TLConstructors.TLInputStickerSetEmpty;

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

        public override string ToString()
        {
            return GetType().Name;
        }

        public override string Name
        {
            get { return @"tlg/empty"; }
        }
    }

    public class TLInputStickerSetId : TLInputStickerSetBase
    {
        public const uint Signature = TLConstructors.TLInputStickerSetId;

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

            Id.ToStream(output);
            AccessHash.ToStream(output);
        }

        public override string ToString()
        {
            return string.Format("{0} Id={1}", GetType().Name, Id);
        }

        public override string Name
        {
            get { return Id.ToString(); }
        }
    }

    public class TLInputStickerSetShortName : TLInputStickerSetBase
    {
        public const uint Signature = TLConstructors.TLInputStickerSetShortName;

        public TLString ShortName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ShortName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ShortName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            ShortName = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            ShortName.ToStream(output);
        }

        public override string ToString()
        {
            return string.Format("{0} ShortName={1}", GetType().Name, ShortName);
        }

        public override string Name
        {
            get { return ShortName.ToString(); }
        }
    }
}
