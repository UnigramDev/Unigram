namespace Telegram.Api.TL.Functions.Messages
{
    public class TLSetTyping : TLObject
    {
        public const string Signature = "#a3825e50";

        public TLInputPeerBase Peer { get; set; }

        public TLSendMessageActionBase Action { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Action.ToBytes());
        }
    }
}
