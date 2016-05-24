namespace Telegram.Api.TL
{
    public class TLFutureSalt : TLObject
    {
        public const uint Signature = TLConstructors.TLFutureSalt;

        public TLInt ValidSince { get; set; }

        public TLInt ValidUntil { get; set; }

        public TLLong Salt { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFutureSalt--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ValidSince = GetObject<TLInt>(bytes, ref position);
            ValidUntil = GetObject<TLInt>(bytes, ref position);
            Salt = GetObject<TLLong>(bytes, ref position);

            TLUtils.WriteLine("ValidSince: " + TLUtils.MessageIdString(ValidSince));
            TLUtils.WriteLine("ValidUntil: " + TLUtils.MessageIdString(ValidUntil));
            TLUtils.WriteLine("Salt: " + Salt);
            
            return this;
        }
    }

    public class TLFutureSalts : TLObject
    {
        public const uint Signature = TLConstructors.TLFutureSalts;

        public TLLong ReqMsgId { get; set; }

        public TLInt Now { get; set; }

        public TLVector<TLFutureSalt> Salts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFutureSalts--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ReqMsgId = GetObject<TLLong>(bytes, ref position);
            Now = GetObject<TLInt>(bytes, ref position);

            TLUtils.WriteLine("ReqMsgId: " + ReqMsgId);
            TLUtils.WriteLine("Now: " + TLUtils.MessageIdString(Now));
            TLUtils.WriteLine("Salts:");

            Salts = GetObject<TLVector<TLFutureSalt>>(bytes, ref position);


            return this;
        }
    }
}
