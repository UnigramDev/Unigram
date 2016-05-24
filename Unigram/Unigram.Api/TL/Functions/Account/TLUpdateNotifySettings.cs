namespace Telegram.Api.TL.Functions.Account
{
    public class TLUpdateNotifySettings : TLObject
    {
        public const string Signature = "#84be5b93";

        public TLInputNotifyPeerBase Peer { get; set; }

        public TLInputPeerNotifySettings Settings { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Settings.ToBytes());
        }
    }
}
