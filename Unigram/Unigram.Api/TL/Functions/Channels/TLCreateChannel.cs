namespace Telegram.Api.TL.Functions.Channels
{
#if LAYER_41
    class TLCreateChannel : TLObject
    {
        public const uint Signature = 0xf4893d7f;

        public TLInt Flags { get; set; }

        public TLString Title { get; set; }

        public TLString About { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Title.ToBytes(),
                About.ToBytes());
        }
    }
#else
    class TLCreateChannel : TLObject
    {
        public const uint Signature = 0x5521d844;

        public TLInt Flags { get; set; }

        public TLString Title { get; set; }

        public TLString About { get; set; }

        public TLVector<TLInputUserBase> Users { get; set; } 

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Title.ToBytes(),
                About.ToBytes(),
                Users.ToBytes());
        }
    }
#endif
}
