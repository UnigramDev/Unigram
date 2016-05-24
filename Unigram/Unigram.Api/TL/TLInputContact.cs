namespace Telegram.Api.TL
{
    public abstract class TLInputContactBase : TLObject { }

    public class TLInputContact : TLInputContactBase
    {
        public const uint Signature = TLConstructors.TLInputContact;

        public TLLong ClientId { get; set; }

        public TLString Phone { get; set; }

        public TLString FirstName { get; set; }

        public TLString LastName { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ClientId.ToBytes(),
                Phone.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes());
        }

        public override string ToString()
        {
            return string.Format("{0} {1}, {2}, {3}", FirstName, LastName, ClientId, Phone);
        }
    }
}
