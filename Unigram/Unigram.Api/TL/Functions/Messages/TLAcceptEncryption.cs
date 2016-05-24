namespace Telegram.Api.TL.Functions.Messages
{
    public class TLAcceptEncryption : TLObject
    {
        public const string Signature = "#3dbc0415";

        public TLInputEncryptedChat Peer { get; set; }

        public TLString GB { get; set; }

        public TLLong KeyFingerprint { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                GB.ToBytes(),
                KeyFingerprint.ToBytes());
        }
    }
 }