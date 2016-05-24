namespace Telegram.Api.TL.Functions.Messages
{
    public class TLReportSpam : TLObject
    {
        public const uint Signature = 0xcf1592db;

        public TLInputPeerBase Peer { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes());
        }
    }
}
