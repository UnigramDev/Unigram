namespace Telegram.Api.TL
{
    public abstract class TLWallPaperBase : TLObject
    {
        public TLInt Id { get; set; }

        public TLString Title { get; set; }

        public TLInt Color { get; set; }
    }

    public class TLWallPaper : TLWallPaperBase
    {
        public const uint Signature = TLConstructors.TLWallPaper;

        public TLVector<TLPhotoSizeBase> Sizes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            Sizes = GetObject<TLVector<TLPhotoSizeBase>>(bytes, ref position);
            Color = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }

    public class TLWallPaperSolid : TLWallPaperBase
    {
        public const uint Signature = TLConstructors.TLWallPaperSolid;

        public TLInt BgColor { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            BgColor = GetObject<TLInt>(bytes, ref position);
            Color = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }
}
