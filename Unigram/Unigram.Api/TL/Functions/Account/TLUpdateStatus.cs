namespace Telegram.Api.TL.Functions.Account
{
    public class TLUpdateStatus : TLObject
    {
        public const string Signature = "#6628562c";

        public TLBool Offline { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offline.ToBytes());
        }
    }
}
