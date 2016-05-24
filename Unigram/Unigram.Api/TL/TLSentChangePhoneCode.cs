using System.Text;

namespace Telegram.Api.TL
{
    public class TLSentChangePhoneCode : TLObject
    {
        public const uint Signature = TLConstructors.TLSentChangePhoneCode;

        public TLString PhoneCodeHash { get; set; }

        public TLInt SendCodeTimeout { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneCodeHash = GetObject<TLString>(bytes, ref position);
            SendCodeTimeout = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneCodeHash.ToBytes(),
                SendCodeTimeout.ToBytes());
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("SentChangePhoneCode");
            sb.AppendLine(string.Format("PhoneCodeHash " + PhoneCodeHash));
            sb.AppendLine(string.Format("SendCodeTimeout " + SendCodeTimeout));

            return sb.ToString();
        }
    }
}
