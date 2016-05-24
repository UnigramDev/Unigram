namespace Telegram.Api.TL
{
    public abstract class TLPhotosBase : TLObject
    {
        public TLVector<TLPhotoBase> Photos { get; set; }

        public TLVector<TLUserBase> Users { get; set; }
    }

    public class TLPhotos : TLPhotosBase
    {
        public const uint Signature = TLConstructors.TLPhotos; 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Photos = GetObject<TLVector<TLPhotoBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }

    public class TLPhotosSlice : TLPhotosBase
    {
        public const uint Signature = TLConstructors.TLPhotosSlice;

        public TLInt Count { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Count = GetObject<TLInt>(bytes, ref position);
            Photos = GetObject<TLVector<TLPhotoBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
