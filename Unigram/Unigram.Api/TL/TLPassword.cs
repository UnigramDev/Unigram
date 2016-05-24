namespace Telegram.Api.TL
{
    public abstract class TLPasswordBase : TLObject
    {
        public TLString NewSalt { get; set; }

        public TLString EmailUnconfirmedPattern { get; set; }

        public abstract bool IsAvailable { get; }

        public string TempNewPassword { get; set; }

        public bool IsAuthRecovery { get; set; }
    }

    public class TLPassword : TLPasswordBase
    {
        public const uint Signature = TLConstructors.TLPassword;

        public TLString CurrentSalt { get; set; }

        public TLString Hint { get; set; }

        public TLBool HasRecovery { get; set; }

        #region Additional
        public TLString CurrentPasswordHash { get; set; }

        public TLPasswordSettings Settings { get; set; }

        public override bool IsAvailable
        {
            get { return true; }
        }

        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            CurrentSalt = GetObject<TLString>(bytes, ref position);
            NewSalt = GetObject<TLString>(bytes, ref position);
            Hint = GetObject<TLString>(bytes, ref position);
            HasRecovery = GetObject<TLBool>(bytes, ref position);
            EmailUnconfirmedPattern = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }

    public class TLNoPassword : TLPasswordBase
    {
        public const uint Signature = TLConstructors.TLNoPassword;

        #region Additional
        public TLString CurrentPasswordHash { get; set; }

        public TLPasswordSettings Settings { get; set; }

        public override bool IsAvailable
        {
            get { return !TLString.IsNullOrEmpty(EmailUnconfirmedPattern); }
        }

        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            NewSalt = GetObject<TLString>(bytes, ref position);
            EmailUnconfirmedPattern = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
