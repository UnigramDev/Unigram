using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLAllStickersBase : TLObject { }

    public class TLAllStickersNotModified : TLAllStickersBase
    {
        public const uint Signature = TLConstructors.TLAllStickersNotModified;

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

    public class TLAllStickers : TLAllStickersBase
    {
        public const uint Signature = TLConstructors.TLAllStickers;

        public TLString Hash { get; set; }

        public TLVector<TLStickerPack> Packs { get; set; } 

        public TLVector<TLDocumentBase> Documents { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLString>(bytes, ref position);
            Packs = GetObject<TLVector<TLStickerPack>>(bytes, ref position);
            Documents = GetObject<TLVector<TLDocumentBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes(),
                Packs.ToBytes(),
                Documents.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Hash = GetObject<TLString>(input);
            Packs = GetObject<TLVector<TLStickerPack>>(input);
            Documents = GetObject<TLVector<TLDocumentBase>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Hash.ToStream(output);
            Packs.ToStream(output);
            Documents.ToStream(output);
        }
    }

    public class TLAllStickers29 : TLAllStickers
    {
        public new const uint Signature = TLConstructors.TLAllStickers29;

        public TLVector<TLStickerSetBase> Sets { get; set; }

        #region Additional
        public TLInt Date { get; set; }

        public TLBool IsDefaultSetVisible { get; set; }

        public TLVector<TLRecentlyUsedSticker> RecentlyUsed { get; set; } 
        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLString>(bytes, ref position);
            Packs = GetObject<TLVector<TLStickerPack>>(bytes, ref position);
            Sets = GetObject<TLVector<TLStickerSetBase>>(bytes, ref position);
            Documents = GetObject<TLVector<TLDocumentBase>>(bytes, ref position);

            IsDefaultSetVisible = new TLBool(true);
            RecentlyUsed = new TLVector<TLRecentlyUsedSticker>();
            Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes(),
                Packs.ToBytes(),
                Sets.ToBytes(),
                Documents.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Hash = GetObject<TLString>(input);
            Packs = GetObject<TLVector<TLStickerPack>>(input);
            Sets = GetObject<TLVector<TLStickerSetBase>>(input);
            Documents = GetObject<TLVector<TLDocumentBase>>(input);

            IsDefaultSetVisible = GetNullableObject<TLBool>(input);
            RecentlyUsed = GetNullableObject<TLVector<TLRecentlyUsedSticker>>(input);
            Date = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Hash.ToStream(output);
            Packs.ToStream(output);
            Sets.ToStream(output);
            Documents.ToStream(output);

            IsDefaultSetVisible.NullableToStream(output);
            RecentlyUsed.NullableToStream(output);
            Date.NullableToStream(output);
        }
    }

    public class TLAllStickers32 : TLAllStickers29
    {
        public new const uint Signature = TLConstructors.TLAllStickers32;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLString>(bytes, ref position);
            Sets = GetObject<TLVector<TLStickerSetBase>>(bytes, ref position);
            
            Packs = new TLVector<TLStickerPack>();
            Documents = new TLVector<TLDocumentBase>();
            IsDefaultSetVisible = TLBool.True;
            RecentlyUsed = new TLVector<TLRecentlyUsedSticker>();
            Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes(),
                Sets.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Hash = GetObject<TLString>(input);
            Packs = GetObject<TLVector<TLStickerPack>>(input);
            Sets = GetObject<TLVector<TLStickerSetBase>>(input);
            Documents = GetObject<TLVector<TLDocumentBase>>(input);

            IsDefaultSetVisible = GetNullableObject<TLBool>(input);
            RecentlyUsed = GetNullableObject<TLVector<TLRecentlyUsedSticker>>(input);
            Date = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Hash.ToStream(output);
            Packs.ToStream(output);
            Sets.ToStream(output);
            Documents.ToStream(output);

            IsDefaultSetVisible.NullableToStream(output);
            RecentlyUsed.NullableToStream(output);
            Date.NullableToStream(output);
        }
    }
}
