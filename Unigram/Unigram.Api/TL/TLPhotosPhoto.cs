namespace Telegram.Api.TL
{
    public class TLPhotosPhoto : TLObject
    {
        public const uint Signature = TLConstructors.TLPhotosPhoto;

        public TLPhotoBase Photo { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Photo = GetObject<TLPhotoBase>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
