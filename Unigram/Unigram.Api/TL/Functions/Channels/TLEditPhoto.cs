namespace Telegram.Api.TL.Functions.Channels
{
    public class TLEditPhoto : TLObject
    {
        public const uint Signature = 0xf12e57c9;

        public TLInputChannelBase Channel { get; set; }

        public TLInputChatPhotoBase Photo { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Photo.ToBytes());
        }
    }
}
