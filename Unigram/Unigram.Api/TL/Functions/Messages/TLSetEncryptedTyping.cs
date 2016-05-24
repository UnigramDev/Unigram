namespace Telegram.Api.TL.Functions.Messages
{
    public class TLSetEncryptedTyping : TLObject
    {
        public const string Signature = "#791451ed";

        public TLInputEncryptedChat Peer { get; set; }

        public TLBool Typing { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Typing.ToBytes());
        }
    }
}