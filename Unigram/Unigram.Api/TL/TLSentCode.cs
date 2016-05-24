using System.Text;

namespace Telegram.Api.TL
{
    public abstract class TLSentCodeBase : TLObject
    {
        public TLBool PhoneRegistered { get; set; }

        public TLString PhoneCodeHash { get; set; }

        public TLInt SendCallTimeout { get; set; }

        public TLBool IsPassword { get; set; }
    }

    public class TLSentCode : TLSentCodeBase
    {
        public const uint Signature = TLConstructors.TLSentCode;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneRegistered = GetObject<TLBool>(bytes, ref position);
            PhoneCodeHash = GetObject<TLString>(bytes, ref position);
            SendCallTimeout = GetObject<TLInt>(bytes, ref position);
            IsPassword = GetObject<TLBool>(bytes, ref position);            

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneRegistered.ToBytes(),
                PhoneCodeHash.ToBytes(),
                SendCallTimeout.ToBytes(),
                IsPassword.ToBytes());
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("SentCode");
            sb.AppendLine(string.Format("PhoneRegistered " + PhoneRegistered));
            sb.AppendLine(string.Format("PhoneCodeHash " + PhoneCodeHash));
            sb.AppendLine(string.Format("SendCallTimeout " + SendCallTimeout));
            sb.AppendLine(string.Format("IsPassword " + IsPassword));

            return sb.ToString();
        }
    }

    public class TLSentAppCode : TLSentCodeBase
    {
        public const uint Signature = TLConstructors.TLSentAppCode;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneRegistered = GetObject<TLBool>(bytes, ref position);
            PhoneCodeHash = GetObject<TLString>(bytes, ref position);
            SendCallTimeout = GetObject<TLInt>(bytes, ref position);
            IsPassword = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneRegistered.ToBytes(),
                PhoneCodeHash.ToBytes(),
                SendCallTimeout.ToBytes(),
                IsPassword.ToBytes());
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("SentAppCode");
            sb.AppendLine(string.Format("PhoneRegistered " + PhoneRegistered));
            sb.AppendLine(string.Format("PhoneCodeHash " + PhoneCodeHash));
            sb.AppendLine(string.Format("SendCallTimeout " + SendCallTimeout));
            sb.AppendLine(string.Format("IsPassword " + IsPassword));

            return sb.ToString();
        }
    }
}
