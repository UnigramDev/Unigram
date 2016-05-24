using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [Flags]
    public enum StickerSetFlags
    {
        Installed = 0x1,
        Disabled = 0x2,
        Official = 0x4,
    }

    public abstract class TLStickerSetBase : TLObject
    {
        public TLLong Id { get; set; }
        public TLLong AccessHash { get; set; }
        public TLString Title { get; set; }
        public TLString ShortName { get; set; }

        #region Additional
        public TLVector<TLObject> Stickers { get; set; }
        #endregion
    }

    public class TLStickerSet : TLStickerSetBase
    {
        public const uint Signature = TLConstructors.TLStickerSet;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            ShortName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                Title.ToBytes(),
                ShortName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            Title = GetObject<TLString>(input);
            ShortName = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            AccessHash.ToStream(output);
            Title.ToStream(output);
            ShortName.ToStream(output);
        }
    }


    public class TLStickerSet32 : TLStickerSet
    {
        public new const uint Signature = TLConstructors.TLStickerSet32;

        public TLInt Flags { get; set; }
        public TLInt Count { get; set; }
        public TLInt Hash { get; set; }

        public bool IsInstalled()
        {
            return IsSet(Flags, (int)StickerSetFlags.Installed);
        }

        public bool IsDisabled()
        {
            return IsSet(Flags, (int)StickerSetFlags.Disabled);
        }

        public bool IsOfficial()
        {
            return IsSet(Flags, (int)StickerSetFlags.Official);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            ShortName = GetObject<TLString>(bytes, ref position);
            Count = GetObject<TLInt>(bytes, ref position);
            Hash = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                Title.ToBytes(),
                ShortName.ToBytes(), 
                Count.ToBytes(),
                Hash.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            Title = GetObject<TLString>(input);
            ShortName = GetObject<TLString>(input);
            Count = GetObject<TLInt>(input);
            Hash = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id.ToStream(output);
            AccessHash.ToStream(output);
            Title.ToStream(output);
            ShortName.ToStream(output);
            Count.ToStream(output);
            Hash.ToStream(output);
        }
    }
}
