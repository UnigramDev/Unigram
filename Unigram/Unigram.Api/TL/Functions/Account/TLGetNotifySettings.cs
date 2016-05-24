namespace Telegram.Api.TL.Functions.Account
{
    public class TLGetNotifySettings : TLObject
    {
        public const string Signature = "#12b3ad31";

        public TLInputNotifyPeerBase Peer { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes());
        }
    }
}
