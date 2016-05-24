namespace Telegram.Api.TL
{
    public abstract class TLServerDHParamsBase : TLObject
    {
        public TLInt128 Nonce { get; set; }

        public TLInt128 ServerNonce { get; set; }
    }

    public class TLServerDHParamsFail : TLServerDHParamsBase
    {
        public const uint Signature = TLConstructors.TLServerDHParamsFail;

        public TLInt128 NewNonceHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Nonce = GetObject<TLInt128>(bytes, ref position);
            ServerNonce = GetObject<TLInt128>(bytes, ref position);
            NewNonceHash = GetObject<TLInt128>(bytes, ref position);           

            return this;
        }
    }

    public class TLServerDHParamsOk : TLServerDHParamsBase
    {
        public const uint Signature = TLConstructors.TLServerDHParamsOk;

        public TLString EncryptedAnswer { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Nonce = GetObject<TLInt128>(bytes, ref position);
            ServerNonce = GetObject<TLInt128>(bytes, ref position);
            EncryptedAnswer = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
